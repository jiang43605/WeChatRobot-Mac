using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;
using System.Diagnostics;


namespace WXLogin
{
    /// <summary>
    /// 微信主要业务逻辑服务类
    /// </summary>
    public class WXService
    {
        private static WXService _wxService;
        private JObject _initResult;
        private IWXMsgHandle _msgHandle;
        private Action<IEnumerable<WXMsg>> _listeningMsgHandle;
        private readonly Dictionary<string, int> _unkownUserNameDic;
        private static readonly Dictionary<string, string> _syncKey = new Dictionary<string, string>();
        private static readonly Object _lockObject = new Object();
        public static readonly ObservableCollection<WXUserViewModel> RecentContactList = new ObservableCollection<WXUserViewModel>();
        public static readonly ObservableCollection<WXUserViewModel> AllContactList = new ObservableCollection<WXUserViewModel>();

        /// <summary>
        /// 尝试获取未知username的次数
        /// </summary>
        private const int TRYNUMOFGETINFO = 3;
        //微信初始化url
        private static string _init_url = "https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxinit?r=" + BaseService.GetTime(10);
        //获取好友头像
        private static string _geticon_url = "https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxgeticon?username=";
        //获取群聊（组）头像
        private static string _getheadimg_url = "https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxgetheadimg?username=";
        //获取好友列表
        private static string _getcontact_url = "https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxgetcontact";
        //同步检查url
        private static string _synccheck_url = "https://webpush.weixin.qq.com/cgi-bin/mmwebwx-bin/synccheck?sid={0}&uin={1}&synckey={2}&r={3}&skey={4}&deviceid={5}";
        //同步url
        private static string _sync_url = "https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxsync?sid=";
        //发送消息url
        private static string _sendmsg_url = "https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxsendmsg?sid=";
        //获取其它用户信息
        private const string _batch_url = "https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxbatchgetcontact?type=ex&r={0}&pass_ticket={1}";
        //初始化状态通知
        private const string _webwxstatusnotify = "https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxstatusnotify?pass_ticket=";
        // login out
        public const string _loginOut_url = "https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxlogout?redirect=1&type=0&skey=";

        public event Func<WXUserViewModel, WXMsg, bool> UpdateMsgToWxUsering;
        public event Func<WXUserViewModel, bool> UpdateRecentContactListing;
        public event Action<string> LoginOutting;

        public static WXService Instance
        {
            get
            {
                if (_wxService == null)
                {
                    try
                    {
                        _wxService = new WXService();
                    }
                    catch (Exception e)
                    {
                        WXService.LoginOut();
                        System.Diagnostics.Debug.WriteLine(e.Message);
                        return null;
                    }
                }

                return _wxService;
            }
        }

        /// <summary>
        /// 本人账号
        /// </summary>
        public WXUser Me { set; get; }

        public IWXMsgHandle MsgHandle
        {
            set => _msgHandle = value;
        }


        public string GetDeviceid
        {
            get
            {
                var r = new Random();
                var flag = r.NextDouble().ToString();

                while (flag.Length < 17)
                {
                    flag = r.NextDouble().ToString();
                }

                return "e" + flag.Substring(2, 15);
            }
        }

        /// <summary>
        /// 获取好友完整列表
        /// </summary>
        public List<WXUser> AllContactCache { get; private set; }

        public WXService()
        {
            _unkownUserNameDic = new Dictionary<string, int>();

            RecentContactList.CollectionChanged += (a, b) =>
            {
                foreach (WXUserViewModel item in b.NewItems)
                {
                    if (RecentContactList.Count(o => o.UserName == item.UserName) > 1)
                    {
                        System.Diagnostics.Debug.WriteLine(item.DisplayName);
                    }
                }

            };

            if (!WxInit()) throw new Exception("init error!");
            else
            {
                Me = new WXUser();
                Me.UserName = _initResult["User"]["UserName"].ToString();
                Me.City = "";
                Me.HeadImgUrl = _initResult["User"]["HeadImgUrl"].ToString();
                Me.NickName = _initResult["User"]["NickName"].ToString();
                Me.Province = "";
                Me.PYQuanPin = _initResult["User"]["PYQuanPin"].ToString();
                Me.RemarkName = _initResult["User"]["RemarkName"].ToString();
                Me.RemarkPYQuanPin = _initResult["User"]["RemarkPYQuanPin"].ToString();
                Me.Sex = _initResult["User"]["Sex"].ToString();
                Me.Signature = _initResult["User"]["Signature"].ToString();

                Initstatusnotify();
            }

            // 初始化默认消息处理器
            this._msgHandle = new MsgHandle(this);
        }
        /// <summary>
        /// 微信初始化
        /// </summary>
        /// <returns></returns>
        private bool WxInit()
        {
            System.Diagnostics.Debug.WriteLine("begin init wx construct");
            string init_json = "{{\"BaseRequest\":{{\"Uin\":\"{0}\",\"Sid\":\"{1}\",\"Skey\":\"{2}\",\"DeviceID\":\"" + GetDeviceid + "\"}}}}";
            Cookie sid = BaseService.GetCookie("wxsid");
            Cookie uin = BaseService.GetCookie("wxuin");

            if (sid != null && uin != null)
            {
                init_json = string.Format(init_json, uin.Value, sid.Value, LoginService.SKey.Replace("@", "%40"));
                byte[] bytes = BaseService.SendPostRequest(_init_url + "&pass_ticket=" + LoginService.Pass_Ticket + "&lang=zh_CN", init_json);

                if (bytes == null)
                {
                    return false;
                }

                string init_str = Encoding.UTF8.GetString(bytes);
                JObject init_result = JsonConvert.DeserializeObject(init_str) as JObject;

                foreach (JObject synckey in init_result["SyncKey"]["List"])  //同步键值
                {
                    _syncKey.Add(synckey["Key"].ToString(), synckey["Val"].ToString());
                }

                // init fail
                if (_syncKey.Count == 0) return false;
                System.Diagnostics.Debug.WriteLine("get _syncKey");

                this._initResult = init_result;
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 初始化和mobile的消息通信
        /// </summary>
        private void Initstatusnotify()
        {
            System.Diagnostics.Debug.WriteLine("begin init Initstatusnotify()");
            var sid = BaseService.GetCookie("wxsid");
            var uin = BaseService.GetCookie("wxuin");
            if (sid == null || uin == null) return;

            var url = _webwxstatusnotify + LoginService.Pass_Ticket;
            var jsonObject = new
            {
                BaseRequest = new
                {
                    DeviceID = GetDeviceid,
                    Sid = sid.Value,
                    Skey = LoginService.SKey,
                    Uin = uin.Value
                },
                ClientMsgId = BaseService.GetTime(),
                Code = 3,
                FromUserName = Me.UserName,
                ToUserName = Me.UserName
            };

            BaseService.SendPostRequest(url, JsonConvert.SerializeObject(jsonObject));
        }
        /// <summary>
        /// 初始化联系人数据
        /// </summary>
        public void InitData()
        {
            System.Diagnostics.Debug.WriteLine("begin init InitLatestContact()");
            InitLatestContact();

            System.Diagnostics.Debug.WriteLine("begin init InitContact()");
            InitContact();
        }
        /// <summary>
        /// 获取好友头像
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public byte[] GetIcon(string username)
        {
            return BaseService.SendGetRequest(_geticon_url + username);
        }
        /// <summary>
        /// 获取微信讨论组头像
        /// </summary>
        /// <param name="usename"></param>
        /// <returns></returns>
        public byte[] GetHeadImg(string usename)
        {
            return BaseService.SendGetRequest(_getheadimg_url + usename);
        }

        /// <summary>
        /// 无重复添加元素到集合，T只支持List和ObservableCollection类型
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="items"></param>
        private void UpdateItemsToListWithoutRepeat<T>(T source, IEnumerable<WXUser> items) where T : class
        {
            if (source is List<WXUser>)
            {
                var temp = source as List<WXUser>;
                foreach (var item in items)
                {
                    var exsitItem = temp.SingleOrDefault(o => o.UserName == item.UserName);
                    if (exsitItem != null)
                    {
                        exsitItem.NickName = item.NickName;
                        exsitItem.MemberList = item.MemberList;
                        exsitItem.RemarkName = item.RemarkName;
                        exsitItem.UserType = item.UserType;
                        continue;
                    }

                    temp.Add(item);
                }
            }
            else if (source is ObservableCollection<WXUserViewModel>)
            {
                var temp = source as ObservableCollection<WXUserViewModel>;
                foreach (var item in items)
                {
                    lock (WXService._lockObject)
                    {
                        var exsitItem = temp.SingleOrDefault(o => o.UserName == item.UserName);
                        if (exsitItem != null)
                        {
                            exsitItem.DisplayName = item.ShowName;
                            exsitItem.UserType = item.UserType;
                            exsitItem.HeadImgUrl = item.HeadImgUrl;
                            return;
                        }

                        // if call InitContact() was before InitLatestContact()
                        // then the following code should be replaced: 
                        // var model = AllContactList.SingleOrDefault(o => o.UserName == item.UserName);
                        var model = RecentContactList.SingleOrDefault(o => o.UserName == item.UserName);
                        // ===================================================================================

                        if (model != null) temp.Add(model);
                        else
                        {
                            temp.Add(new WXUserViewModel
                            {
                                DisplayName = item.ShowName,
                                UserType = item.UserType,
                                HeadImgUrl = item.HeadImgUrl,
                                UserName = item.UserName
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 上面方法的异步版本
        /// </summary>
        /// <param name="source">Source.</param>
        /// <param name="items">Items.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        private async void UpdateItemsToListWithoutRepeatAsync<T>(T source, IEnumerable<WXUser> items) where T : class
        {
            await Task.Run(() => UpdateItemsToListWithoutRepeat(source, items));
        }

        /// <summary>
        /// 获取好友列表，必须在初始化最近好友之后
        /// </summary>
        /// <returns></returns>
        private void InitContact()
        {
            var bytes = BaseService.SendGetRequest(_getcontact_url);
            var contact_str = Encoding.UTF8.GetString(bytes);

            var contact_result = JsonConvert.DeserializeObject(contact_str) as JObject;
            var contact_all = new List<WXUser>();

            if (contact_result == null)
            {
                AllContactCache = contact_all;
                return;
            }

            contact_all.AddRange(from JObject contact in contact_result["MemberList"]
                                 select new WXUser
                                 {
                                     UserType = WXUser.GetUserType(contact["UserName"].ToString(), contact["VerifyFlag"].ToString()),
                                     UserName = contact["UserName"].ToString(),
                                     City = contact["City"].ToString(),
                                     HeadImgUrl = contact["HeadImgUrl"].ToString(),
                                     NickName = contact["NickName"].ToString(),
                                     Province = contact["Province"].ToString(),
                                     PYQuanPin = contact["PYQuanPin"].ToString(),
                                     RemarkName = contact["RemarkName"].ToString(),
                                     DisplayName = contact["DisplayName"].ToString(),
                                     RemarkPYQuanPin = contact["RemarkPYQuanPin"].ToString(),
                                     Sex = contact["Sex"].ToString(),
                                     Signature = contact["Signature"].ToString(),
                                     ContactFlag = contact["ContactFlag"].Value<int>(),
                                     Statues = contact["Statues"].Value<int>()
                                 });

            AllContactCache = contact_all;

            UpdateItemsToListWithoutRepeat(AllContactList, contact_all);
        }
        /// <summary>
        /// 获取所有好友列表,在初次初始化时候被调用
        /// </summary>
        /// <param name="msg"></param>
        private async void InitAllContactAsync(string msg)
        {
            await Task.Run(() =>
            {
                var allList = msg.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(o => o.StartsWith("@"))
                    .Where(o => AllContactCache.All(k => k.UserName != o));

                if (!allList.Any()) return;
                lock (AllContactCache)
                {
                    UpdateItemsToListWithoutRepeat(AllContactCache, GetBatchContact(allList));
                }
            });
        }
        /// <summary>
        /// 获取最近好友列表
        /// </summary>
        private void InitLatestContact()
        {
            if (this._initResult == null) return;
            var list = _initResult["ContactList"].Select(contact =>
            {
                var newUser = new WXUser
                {
                    UserType = WXUser.GetUserType(contact["UserName"].ToString(), contact["VerifyFlag"].ToString()),
                    UserName = contact["UserName"].ToString(),
                    City = contact["City"].ToString(),
                    HeadImgUrl = contact["HeadImgUrl"].ToString(),
                    NickName = contact["NickName"].ToString(),
                    Province = contact["Province"].ToString(),
                    PYQuanPin = contact["PYQuanPin"].ToString(),
                    RemarkName = contact["RemarkName"].ToString(),
                    DisplayName = contact["DisplayName"].ToString(),
                    RemarkPYQuanPin = contact["RemarkPYQuanPin"].ToString(),
                    Sex = contact["Sex"].ToString(),
                    Signature = contact["Signature"].ToString(),
                    ContactFlag = contact["ContactFlag"].Value<int>(),
                    Statues = contact["Statues"].Value<int>()
                };

                if (newUser.UserName.StartsWith("@@")) newUser.UserType = UserType.ChatRoom;
                return newUser;
            }).ToList();

            System.Diagnostics.Debug.WriteLine("InitLatestContact");
            System.Diagnostics.Debug.WriteLine("get first laster friends");
            UpdateItemsToListWithoutRepeat(RecentContactList, list);

            Task.Run(() =>
            {
                // check other in ChatSet
                var chatset = _initResult["ChatSet"].Value<string>();
                var otherItems = chatset.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(o => o.StartsWith("@@"));

                System.Diagnostics.Debug.WriteLine("get other in chatset");
                var newItems = GetBatchContact(otherItems);
                UpdateItemsToListWithoutRepeat(RecentContactList, newItems);

                // update all
                list.AddRange(newItems);
                UpdateItemsToListWithoutRepeat(AllContactCache, list);
            });
        }
        /// <summary>
        /// get public friend and charoom
        /// </summary>
        /// <param name="usernames"></param>
        /// <returns></returns>
        private List<WXUser> GetBatchContact(IEnumerable<string> usernames)
        {
            var sid = BaseService.GetCookie("wxsid");
            var uin = BaseService.GetCookie("wxuin");
            if (sid == null || uin == null) return null;

            // build the parameter
            var url = string.Format(_batch_url, BaseService.GetTime(), LoginService.Pass_Ticket);
            var us = usernames as string[] ?? usernames.ToArray();
            var jsonObject = new
            {
                BaseRequest = new
                {
                    DeviceID = GetDeviceid,
                    Sid = sid.Value,
                    Skey = LoginService.SKey,
                    Uin = uin.Value
                },
                Count = us.Length,
                List = us.Select(o => new
                {
                    EncryChatRoomId = string.Empty,
                    UserName = o
                })
            };
            var jsonString = JsonConvert.SerializeObject(jsonObject);

            // requst http
            var byteInfos = BaseService.SendPostRequest(url, jsonString);
            var jObject = JToken.Parse(Encoding.UTF8.GetString(byteInfos));

            System.Diagnostics.Debug.WriteLine($"ContactList:" + jObject["Count"]);
            return jObject["ContactList"].Select(o =>
            {
                var wxUser = new WXUser
                {
                    UserType = WXUser.GetUserType(o["UserName"].ToString(), o["VerifyFlag"].ToString()),
                    UserName = o["UserName"].Value<string>(),
                    NickName = o["NickName"].Value<string>(),
                    Signature = o["Signature"].Value<string>(),
                    HeadImgUrl = o["HeadImgUrl"].Value<string>(),
                    PYQuanPin = o["PYQuanPin"].Value<string>(),
                    RemarkPYQuanPin = o["RemarkPYQuanPin"].Value<string>(),
                    ContactFlag = o["ContactFlag"].Value<int>(),
                    Statues = o["Statues"].Value<int>()
                };

                if (wxUser.UserName.StartsWith("@@"))
                {
                    wxUser.RemarkName = o["RemarkName"].Value<string>();
                    wxUser.MemberList = o["MemberList"].Select(k => new WXUser
                    {
                        DisplayName = k["DisplayName"].Value<string>(),
                        NickName = k["NickName"].Value<string>(),
                        PYQuanPin = k["PYQuanPin"].Value<string>(),
                        UserName = k["UserName"].Value<string>()
                    }).ToList();

                    return wxUser;
                }
                if (wxUser.UserName.StartsWith("@"))
                {
                    return wxUser;
                }

                System.Diagnostics.Debug.WriteLine($"unkown useName: " + wxUser.UserName);
                return null;
            }).Where(o => o != null).ToList();
        }
        /// <summary>
        /// 微信同步检测
        /// </summary>
        /// <returns></returns>
        private string WxSyncCheck()
        {
            string sync_key = "";
            foreach (KeyValuePair<string, string> p in _syncKey)
            {
                sync_key += p.Key + "_" + p.Value + "%7C";
            }
            sync_key = sync_key.TrimEnd('%', '7', 'C');

            Cookie sid = BaseService.GetCookie("wxsid");
            Cookie uin = BaseService.GetCookie("wxuin");

            if (sid != null && uin != null)
            {
                var checkurl = string.Format(_synccheck_url,
                                             Uri.EscapeUriString(sid.Value),
                                             uin.Value,
                                             sync_key,
                                             (long)(DateTime.Now.ToUniversalTime() - new System.DateTime(1970, 1, 1)).TotalMilliseconds,
                                             Uri.EscapeUriString(LoginService.SKey),
                                             GetDeviceid
                                            );
                checkurl += "&_=" + BaseService.GetTime();

                var ckc = new CookieCollection();
                foreach (var item in BaseService.GetAllCookies(BaseService.CookiesContainer))
                {
                    var ck = new Cookie(item.Name, item.Value, "/", ".qq.com");
                    ckc.Add(ck);
                }

                byte[] bytes = BaseService.SendGetRequest(checkurl, ckc);
                if (bytes != null)
                {
                    return Encoding.UTF8.GetString(bytes);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// 微信同步
        /// </summary>
        /// <returns></returns>
        private JObject WxSync()
        {
            string sync_json = "{{\"BaseRequest\" : {{\"DeviceID\":\"" + GetDeviceid + "\",\"Sid\":\"{1}\", \"Skey\":\"{5}\", \"Uin\":\"{0}\"}},\"SyncKey\" : {{\"Count\":{2},\"List\":[{3}]}},\"rr\" :{4}}}";
            Cookie sid = BaseService.GetCookie("wxsid");
            Cookie uin = BaseService.GetCookie("wxuin");

            if (sid != null && uin != null)
            {
                string sync_keys = "";
                foreach (KeyValuePair<string, string> p in _syncKey)
                {
                    sync_keys += "{\"Key\":" + p.Key + ",\"Val\":" + p.Value + "},";
                }
                sync_keys = sync_keys.TrimEnd(',');
                sync_json = string.Format(sync_json, uin.Value, sid.Value, _syncKey.Count, sync_keys, (long)(DateTime.Now.ToUniversalTime() - new System.DateTime(1970, 1, 1)).TotalMilliseconds, LoginService.SKey);

                byte[] bytes = BaseService.SendPostRequest(_sync_url + sid.Value + "&lang=zh_CN&skey=" + LoginService.SKey + "&pass_ticket=" + LoginService.Pass_Ticket, sync_json);
                if (bytes == null) return null;
                string sync_str = Encoding.UTF8.GetString(bytes);

                JObject sync_resul = JsonConvert.DeserializeObject(sync_str) as JObject;

                if (sync_resul["SyncKey"]["Count"].ToString() != "0")
                {
                    _syncKey.Clear();
                    foreach (JObject key in sync_resul["SyncKey"]["List"])
                    {
                        _syncKey.Add(key["Key"].ToString(), key["Val"].ToString());
                    }
                }
                return sync_resul;
            }

            return null;
        }

        /// <summary>
        /// 发送消息，暂时只支持文本格式
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="type"></param>
        public void SendMsg(string msg, string from, string to, int type)
        {
            string msg_json = "{{" +
            "\"BaseRequest\":{{" +
                "\"DeviceID\" : \"" + GetDeviceid + "\"," +
                "\"Sid\" : \"{0}\"," +
                "\"Skey\" : \"{6}\"," +
                "\"Uin\" : \"{1}\"" +
            "}}," +
            "\"Msg\" : {{" +
                "\"ClientMsgId\" : {8}," +
                "\"Content\" : \"{2}\"," +
                "\"FromUserName\" : \"{3}\"," +
                "\"LocalID\" : {9}," +
                "\"ToUserName\" : \"{4}\"," +
                "\"Type\" : {5}" +
            "}}," +
            "\"rr\" : {7}" +
            "}}";

            Cookie sid = BaseService.GetCookie("wxsid");
            Cookie uin = BaseService.GetCookie("wxuin");

            if (sid != null && uin != null)
            {
                var msgItem = new WXMsg
                {
                    From = from,
                    To = to,
                    Msg = msg,
                    OriginMsg = msg,
                    Type = type,
                    FromUserInfo = Me,
                    ToUserInfo = this.GetWXUserInfo(to),
                    Time = DateTime.Now,
                    Readed = true
                };

                UpdateLatestContactAsync(new[] { msgItem });

                msg_json = string.Format(msg_json, sid.Value, uin.Value, msg, from, to, type, LoginService.SKey, DateTime.Now.Millisecond, DateTime.Now.Millisecond, DateTime.Now.Millisecond);

                /*byte[] bytes = */
                BaseService.SendPostRequest(_sendmsg_url + sid.Value + "&lang=zh_CN&pass_ticket=" + LoginService.Pass_Ticket, msg_json);
                //string send_result = Encoding.UTF8.GetString(bytes);

                // 支持手动发送，消息能被监听到
                this._listeningMsgHandle?.Invoke(new[] { msgItem });
            }
        }

        public async void SendMsgAsync(string msg, string from, string to, int type)
        {
            await Task.Run(() =>
            {
                SendMsg(msg, from, to, type);
            });
        }
        /// <summary>
        /// 退出
        /// </summary>
        public static void LoginOut()
        {
            var url = _loginOut_url + LoginService.SKey.Replace("@", "%40");

            Cookie sid = BaseService.GetCookie("wxsid");
            Cookie uin = BaseService.GetCookie("wxuin");

            if (sid != null && uin != null)
            {
                if (BaseService.CookiesContainer == null) BaseService.CookiesContainer = new CookieContainer();

                var ckc = new CookieCollection();
                foreach (var item in BaseService.GetAllCookies(BaseService.CookiesContainer))
                {
                    var ck = new Cookie(item.Name, item.Value, "/", ".qq.com");
                    ckc.Add(ck);
                }

                BaseService.SendPostRequest(url, $"sid={sid.Value}&uin={uin.Value}", true, ckc);
            }
        }
        /// <summary>
        /// get nickName from userName
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public string GetNickName(string userName, string msg)
        {
            var un = RecentContactList.SingleOrDefault(o => o.UserName == userName);

            if (un != null)
            {
                if (un.UserType != UserType.ChatRoom) return un.DisplayName;

                var unFromAll = AllContactCache.SingleOrDefault(o => o.UserName == userName);
                if (unFromAll == null) return $"[{un.DisplayName}][unkown]";
                var splitString = msg.Split(new[] { ":<br/>" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
                return $"[{un.DisplayName}]{unFromAll.MemberList.SingleOrDefault(o => o.UserName == splitString)?.ShowName}";
            }

            var unFromAll1 = AllContactCache.SingleOrDefault(o => o.UserName == userName);
            if (unFromAll1 == null)
            {
                if (!_unkownUserNameDic.ContainsKey(userName)) _unkownUserNameDic.Add(userName, 0);
                if (_unkownUserNameDic[userName] > TRYNUMOFGETINFO) return "[unkown]";
                Task.Run(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"[{_unkownUserNameDic[userName] + 1}]Try to get user info from " + userName);
                    var unkownUserInfo = GetBatchContact(new[] { userName }).FirstOrDefault();
                    if (unkownUserInfo != null)
                        UpdateItemsToListWithoutRepeat(AllContactCache, new[] { unkownUserInfo });
                    _unkownUserNameDic[userName] += 1;
                });

                return "[unkown]";
            }
            if (unFromAll1.UserType != UserType.ChatRoom) return unFromAll1.ShowName;
            var splitString1 = msg.Split(new[] { ":<br/>" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            return $"[{unFromAll1.ShowName}]{unFromAll1.MemberList?.SingleOrDefault(o => o.UserName == splitString1)?.ShowName}";
        }

        /// <summary>
        /// 获取用户的信息
        /// 如果没有，将从服务器拉取
        /// </summary>
        /// <returns>The user.</returns>
        /// <param name="userName">User name.</param>
        public WXUser GetWXUserInfo(string userName)
        {
            var un = AllContactCache.SingleOrDefault(o => o.UserName == userName);

            if (un == null)
            {
                if (!_unkownUserNameDic.ContainsKey(userName)) _unkownUserNameDic.Add(userName, 0);

                System.Diagnostics.Debug.WriteLine($"[{_unkownUserNameDic[userName] + 1}]Try to get user info from " + userName);
                var unkownUserInfo = GetBatchContact(new[] { userName }).FirstOrDefault();
                if (unkownUserInfo != null)
                    UpdateItemsToListWithoutRepeatAsync(AllContactCache, new[] { unkownUserInfo });

                return unkownUserInfo;
            }

            return un;
        }

        /// <summary>
        /// 转换消息或昵称中的表情标签
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static string DecodeMsgFace(string msg)
        {
            if (msg == null) return null;
            return Regex.Replace(msg, "<span class=\"emoji emoji([a-zA-Z0-9]+)\"></span>", match =>
            {
                var emojiValue = match.Groups[1].Value;
                if (!WXFace.Face.ContainsKey(emojiValue)) return "[表情]";

                return $"[{WXFace.Face[emojiValue].Trim('<', '>')}]";
            });
        }

        public static string HtmlEncode(string html)
        {
            return html?.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("'", "&#39;").Replace("<", "&lt;")
                .Replace(">", "&gt");
        }

        public static string HtmlDecode(string html)
        {
            return html?.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&#39;", "'").Replace("&quot;", "\"")
                .Replace("&amp;", "&");
        }
        /// <summary>
        /// update the RecentContactList by msg
        /// </summary>
        /// <param name="items"></param>
        public async void UpdateLatestContactAsync(IEnumerable<WXMsg> items)
        {
            await Task.Run(() =>
            {
                foreach (var item in items)
                {
                    foreach (var user in new[] { item.From, item.To })
                    {
                        System.Diagnostics.Debug.WriteLine("update user: " + user);
                        if (RecentContactList.Any(o => o.UserName == user)) continue;
                        var newUser = AllContactList.SingleOrDefault(o => o.UserName == user);
                        if (newUser == null)
                        {
                            var newUserSearchInAllContact = AllContactCache.SingleOrDefault(o => o.UserName == user);
                            if (newUserSearchInAllContact == null) return;
                            newUser = new WXUserViewModel
                            {
                                DisplayName = newUserSearchInAllContact.ShowName,
                                HeadImgUrl = newUserSearchInAllContact.HeadImgUrl,
                                UserName = newUserSearchInAllContact.UserName,
                                UserType = newUserSearchInAllContact.UserType
                            };
                        }

                        System.Diagnostics.Debug.WriteLine("update user begin: " + newUser.DisplayName + ";" + newUser.UserName);
                        lock (WXService._lockObject)
                        {

                            if (RecentContactList.Any(o => o.UserName == newUser.UserName)) return;
                            var eventResult = UpdateRecentContactListing?.Invoke(newUser);
                            if (eventResult != null && eventResult != true) return;
                            WXService.RecentContactList.Add(newUser);
                            System.Diagnostics.Debug.WriteLine("update user complete: " + newUser.DisplayName + ";" + newUser.UserName);
                        };
                    }

                    // must use SynchronizationContext
                    lock (WXService._lockObject)
                    {
                        var user = RecentContactList
                            .FirstOrDefault(o => new[] { item.From, item.To }.Where(k => k != Me.UserName).Any(k => k == o.UserName));
                        var status = UpdateMsgToWxUsering?.Invoke(user, item);
                        if (status == false) return;
                        user?.Messages.Add(item);

                    };
                }

            });
        }
        /// <summary>
        /// 监听消息，注意，这会阻塞当前线程
        /// </summary>
        /// <param name="msgAction"></param>
        public void Listening(Action<IEnumerable<WXMsg>> msgAction)
        {
            this._listeningMsgHandle = msgAction;

            while (true)
            {
                var sync_flag = WxSyncCheck();
                var selector = Regex.Match(sync_flag ?? string.Empty, "selector:\"\\s*?(\\d+)\\s*?\"").Groups[1].Value;
                var retcode = Regex.Match(sync_flag ?? string.Empty, "retcode:\"\\s*?(\\d+)\\s*?\"").Groups[1].Value;
                /*
                 * you can do more logic by such info
                 * retcode:
                 *  0 正常
                 *  1100 失败/退出微信
                 * selector:
                 *  0 正常
                 *  2 新的消息
                 *  7 进入/离开聊天界面
                 */

                if (retcode == "1101")
                {
                    WXService.LoginOut();
                    this.LoginOutting?.Invoke("1101");
                    Console.WriteLine("退出");
                    break;
                }

                if (selector == string.Empty || selector == "0")
                {
                    continue;
                }

                var sync_result = WxSync();
                if (sync_result == null) continue;
                if (sync_result["AddMsgCount"] == null || sync_result["AddMsgCount"].ToString() == "0") continue;

                Debug.WriteLine("消息进入");
                Task.Run(() =>
                {
                    var numFlag = default(int);
                    var msgs = sync_result["AddMsgList"].Select(m =>
                    {
                        var fromUserName = m["FromUserName"].ToString();
                        var toUserName = m["ToUserName"].ToString();
                        var msg = m["Content"].ToString();
                        var msgType = m["MsgType"].ToString();
                        var pureMsg = _msgHandle == null ? msg : _msgHandle.Handle(m);

                        var formatMsg = pureMsg is string
                                        ? pureMsg as string
                                            : pureMsg is Tuple<byte[], string>
                                        ? (pureMsg as Tuple<byte[], string>).Item2
                                            : null;

                        var picMsg = pureMsg is Tuple<byte[], string> ? (pureMsg as Tuple<byte[], string>).Item1 : null;

                        return new WXMsg
                        {
                            From = fromUserName,
                            FromUserInfo = this.GetWXUserInfo(fromUserName),
                            OriginMsg = msg,
                            Msg = formatMsg,
                            PicMsg = picMsg,
                            Readed = false,
                            Time = DateTime.Now,
                            To = toUserName,
                            ToUserInfo = this.GetWXUserInfo(toUserName),
                            Type = int.Parse(msgType)
                        };
                    });

                    numFlag = msgs.Count();
                    foreach (var msg in sync_result["AddMsgList"])
                    {
                        if (msg["MsgType"].Value<int>() != 51) continue;

                        Debug.WriteLine("get a msg which the type=51");
                        InitAllContactAsync(msg["StatusNotifyUserName"].Value<string>());
                        numFlag--;
                    }

                    if (numFlag == 0) return;
                    var filterMsg = msgs.Where(o => o.Type != 51);
                    UpdateLatestContactAsync(filterMsg);

                    Debug.WriteLine("开始消息处理");
                    msgAction?.Invoke(filterMsg);
                    Debug.WriteLine("消息处理完毕");
                });
            }
        }
    }


    [Serializable]
    public class LoginOutException : Exception
    {
        public LoginOutException() { }
        public LoginOutException(string message) : base(message) { }
        public LoginOutException(string message, Exception inner) : base(message, inner) { }
        protected LoginOutException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
