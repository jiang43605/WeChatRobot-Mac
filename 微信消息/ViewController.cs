using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using WXLogin;
using AppKit;
using Foundation;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace 微信消息
{
    public partial class ViewController : NSViewController
    {
        private static string preMsg;
        private WXService _wxService;
        private readonly int HOTCONTRACTLIMIT = 5;
        private Stack<KeyValuePair<string, string>> _hotContract;
        private WXUser _currentContract;


        public ViewController(IntPtr handle) : base(handle)
        {
            this._hotContract = new Stack<KeyValuePair<string, string>>();
        }

        /// <summary>
        /// Appends to hot dic. 有数量限制，不超过5个暂定
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="value">Value.</param>
        private void AppendToHotDic(string key, string value)
        {

            if (this._hotContract.Any(o => o.Key == key)) return;

            if (this._hotContract.Count > HOTCONTRACTLIMIT)
            {
                var other = this._hotContract.Take(HOTCONTRACTLIMIT - 1).ToArray();
                this._hotContract.Clear();

                foreach (var item in other) this._hotContract.Append(item);
            }

            this._hotContract.Push(new KeyValuePair<string, string>(key, value));

            this.SetLabelInputDisplayString();
        }

        /// <summary>
        /// Gets the label input display string.
        /// 数据来源_hotContract，使用“，”分隔数据
        /// </summary>
        /// <returns>The label input display string.</returns>
        private void SetLabelInputDisplayString()
        {
            this.BeginInvokeOnMainThread(() =>
            {
                var hotContract = string.Join("，", this._hotContract.Select((o, index) => $"{index + 1}.{o.Value}"));

                this.LabelInputDisplay.StringValue = $"最近联系-[{hotContract}]";
            });

        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            this.TextPrint.Delegate = new TextViewDelegate();

            this.InitLogin();

            if (!Directory.Exists(Config.PICSAVEDIRECTORY)) Directory.CreateDirectory(Config.PICSAVEDIRECTORY);

            // debug: AddImageToTextView(File.ReadAllBytes("/Users/jiangweicheng/Desktop/WechatIMG24219.png"));
        }


        public override NSObject RepresentedObject
        {
            get
            {
                return base.RepresentedObject;
            }
            set
            {
                base.RepresentedObject = value;
                // Update the view, if already loaded.
            }
        }

        private void LoginOutting(string type)
        {
            this.InvokeOnMainThread(() =>
            {
                Dialog("被服务器退出，点击确定后将退出软件");
                this.View.Window.Close();
                Process.GetCurrentProcess().Close();
            });
        }

        /// <summary>
        /// Inits the login.
        /// </summary>
        public async void InitLogin()
        {
            await Task.Run(() =>
            {
                this._wxService = WXService.Instance;
                var me = this._wxService.Me.NickName;
                this.PrintLoginInfo(me);

                this._wxService.LoginOutting += LoginOutting;
                this._wxService.InitData();

                var allFriend = this._wxService.AllContactCache;

                this._wxService.Listening(msgs =>
                {
                    foreach (var msg in msgs) this.PrintMsgToUI(msg, me);
                });
            });
        }

        private void PrintMsgToUI(WXMsg msg, string me)
        {
            var time = msg.Time.ToString("HH:mm:ss");
            var fromShowName = msg.FromUserInfo.ShowName;
            var toShowName = msg.ToUserInfo.ShowName;
            //msg.FromNickName = Regex.Replace(msg.FromNickName, @"\[unkown\]$", string.Empty);
            //var mt = Regex.Match(msg.FromNickName, @"^\[(.*)\](.*)$");
            //var eqNickName = msg.FromNickName;
            //if (mt.Success) eqNickName = mt.Groups[1].Value;

            // 存储当前第一次会话为第一个联系人
            if (this._currentContract == null && !msg.FromNickName.Equals(me))
                this._currentContract = new WXUser { UserName = msg.From, NickName = fromShowName };

            if (!msg.FromNickName.Equals(me))
                this.AppendToHotDic(msg.From, fromShowName);

            if (fromShowName.Equals(me) && !msg.To.Equals(preMsg))
            {
                // 如果消息来源是“我”，则以消息接收方为头
                PrintLine();
                PrintLineHead(toShowName + "-" + time);
            }
            else if (!msg.From.Equals(preMsg) && !fromShowName.Equals(me))
            {
                PrintLine();
                PrintLineHead(fromShowName + "-" + time);
            }

            var name = fromShowName.Equals(me) ? "我" : fromShowName;
            if (msg.FromUserInfo.UserType == UserType.ChatRoom)
            {
                // 只有讨论组才取值
                name = fromShowName.Equals(me) ? "我" : msg.FromMemberUserName ?? "[unkown]";
            }

            if (msg.Type == 3 || (msg.Type == 47 && msg.PicMsg != null))
            {
                // 图片
                PrintLine($"[{name}][{time}]");
                this.AddImageToTextView(msg.PicMsg, msg.Msg);
                PrintLine();
            }
            else
            {
                // 文字
                PrintLine($"[{name}][{time}]{msg.Msg}");
            }

            if (!fromShowName.Equals(me)) preMsg = msg.From;
            else preMsg = msg.To;

            // 发送mac通知
            if (!fromShowName.Equals(me) && !msg.FromUserInfo.IsMuted())
                Tool.SendNotification(fromShowName, msg.Msg, new NSDictionary("userName", msg.From));
        }


        public void PrintLine(string line = "", NSDictionary nsDic = null)
        {
            this.InvokeOnMainThread(() =>
            {
                var appendLine = (NSAttributedString)null;
                if (nsDic == null) appendLine = new NSAttributedString($"{line}\r\n");
                else appendLine = new NSAttributedString($"{line}\r\n", nsDic);

                this.TextPrint.TextStorage.Append(appendLine);
                this.TextPrint.ScrollRangeToVisible(new NSRange(this.TextPrint.String.Length, 0));
            });
        }

        public void PrintLineHead(string line)
        {
            this.InvokeOnMainThread(() =>
            {
                var dic = new NSDictionary(
                   NSStringAttributeKey.ForegroundColor,
                   NSColor.Black,
                   NSStringAttributeKey.Font,
                   NSFont.BoldSystemFontOfSize(NSFont.SystemFontSize)
                );

                this.PrintLine(line, dic);
            });

        }

        public void PrintLoginInfo(string info)
        {
            this.BeginInvokeOnMainThread(() =>
             {
                 this.LoginInfo.Hidden = false;
                 this.LoginInfo.StringValue = info;
             });
        }

        /// <summary>
        /// 弹出对话窗口
        /// </summary>
        /// <param name="title">Title.</param>
        /// <param name="content">Content.</param>
        private void Dialog(string content, string title = "提示")
        {
            this.InvokeOnMainThread(() =>
            {
                var alert = new NSAlert()
                {
                    AlertStyle = NSAlertStyle.Informational,
                    InformativeText = content,
                    MessageText = title,
                };
                alert.RunModal();
            });

        }


        partial void InputChange(NSObject sender)
        {
            var input = sender as NSTextField;

            if (string.IsNullOrWhiteSpace(input.StringValue)) return;
            if (!this.InputStringFilter(input.StringValue)) return;


            if (this._currentContract != null)
                this._wxService.SendMsgAsync(
                    input.StringValue,
                    this._wxService.Me.UserName,
                    this._currentContract.UserName,
                    1
                );

            input.StringValue = string.Empty;
        }

        /// <summary>
        /// 插入图片到textView
        /// </summary>
        /// <param name="bytes">Stream.</param>
        private void AddImageToTextView(byte[] bytes, string originSizeUrl)
        {
            this.InvokeOnMainThread(() =>
            {
                using (var stream = new MemoryStream(bytes))
                {
                    ImageCell cell = new ImageCell(NSImage.FromStream(stream));
                    cell.SetData("picUrl", originSizeUrl);

                    NSTextAttachment am = new NSTextAttachment
                    {
                        AttachmentCell = cell
                    };
                    NSAttributedString nbs = NSAttributedString.FromAttachment(am);
                    this.TextPrint.TextStorage.Append(nbs);
                }
            });
        }

        /// <summary>
        /// 处理用户输入的特殊字符串
        /// Inputs the string filter.
        /// </summary>
        /// <param name="stringValue">String value.</param>
        private bool InputStringFilter(string stringValue)
        {
            if (Regex.IsMatch(stringValue, @"^\d[:：]$"))
            {
                var index = int.Parse(stringValue[0].ToString());
                var user = this._hotContract.ElementAt(index - 1);
                this._currentContract = new WXUser
                {
                    UserName = user.Key,
                    NickName = user.Value
                };

                this.LoginInfo.StringValue = $"当前联系：{this._currentContract.NickName}";
                return false;
            }

            return true;
        }
    }


    public class ButtonCell : NSTextAttachmentCell
    {
    }
}
