using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WXLogin
{
    /// <summary>
    /// 消息格式处理
    /// 依赖BaseService,LoginService
    /// 如果要分离出去，请解耦BaseService,LoginService
    /// </summary>
    public class MsgHandle : IWXMsgHandle
    {
        protected readonly WXService _wxService;

        public MsgHandle(WXService ws)
        {
            _wxService = ws;
        }

        /// <summary>
        /// 返回的格式如下：
        /// string:文本文件
        /// byte[]:图片
        /// </summary>
        /// <returns>The handle.</returns>
        /// <param name="msgInfo">Message info.</param>
        public virtual object Handle(JToken msgInfo)
        {
            var msg = msgInfo["Content"].ToString();
            var msgType = msgInfo["MsgType"].ToString();
            var appMsgType = msgInfo["AppMsgType"].ToString();
            var subMsgType = msgInfo["SubMsgType"].ToString();
            var fromUserName = msgInfo["FromUserName"].ToString();
            var msgId = msgInfo["MsgId"].ToString();
            var hasProductId = msgInfo["HasProductId"].Value<int>();

            if (fromUserName.StartsWith("@@"))
            {
                msg = Regex.Replace(msg, @"^(@[a-zA-Z0-9]+|[a-zA-Z0-9_-]+):<br/>", string.Empty);
            }

            // text msg
            if (msgType == "1")
            {
                if (subMsgType == "48")
                    return $"对方给你发来位置信息: {msg.Split(new[] { ":<br/>" }, StringSplitOptions.RemoveEmptyEntries)[0]}";
                if (fromUserName.StartsWith("@@")) // 群消息
                    return WXService.DecodeMsgFace(msg);
                return fromUserName.Equals("newsapp") ? "[腾讯新闻消息]" : WXService.DecodeMsgFace(msg.Replace("<br/>", string.Empty));
            }

            // maybe from brand contact
            if (msgType == "49")
            {
                if (appMsgType == "5")
                {
                    // brandContact msg
                    var msged = _wxService.AllContactCache.Single(o => o.UserName == fromUserName).UserType != UserType.BrandContact ?
                        "[链接]: " : "来自公众号的消息: ";
                    var el = System.Xml.Linq.XElement.Parse(WXService.HtmlDecode(msg).Replace("<br/>", string.Empty));
                    var allItems = el.Element("appmsg")?.Element("mmreader")?.Element("category")?.Elements("item");
                    if (allItems == null)
                    {
                        msged += "\r\nTitle: " + WXService.DecodeMsgFace(el.Element("appmsg")?.Element("title")?.Value);
                        msged += "\r\nDigest: " + WXService.DecodeMsgFace(el.Element("appmsg")?.Element("des")?.Value);
                        //msged += "\r\nUrl: " + DecodeMsgFace(el.Element("appmsg")?.Element("url")?.Value);

                        return msged;
                    }

                    foreach (var item in allItems)
                    {
                        var title = WXService.DecodeMsgFace(item.Element("title")?.Value);
                        //var url = item.Element("url")?.Value;
                        var digest = WXService.DecodeMsgFace(item.Element("digest")?.Value);

                        msged += $"\r\nTitle: [{title}]\r\nDigest: [{digest}]"/*\r\nUrl: [{url}]*/;
                    }

                    return msged;
                }

                switch (appMsgType)
                {
                    case "2000":
                        return "[对方向你转账消息]";
                }
            }

            var object_47 = new object();
            if (msgType == "47")
            {
                // 等于1时候，为微信商店表情
                if (hasProductId == 1) object_47 = "[收到了一个表情，请在手机上查看]";
                else object_47 = this.OnHandlePic(msgType, msgId, LoginService.SKey);
            }

            // other msg
            switch (msgType)
            {
                case "3":
                    return this.OnHandlePic(msgType, msgId, LoginService.SKey);
                case "47":
                    return object_47;
                case "34":
                    return "[语音消息]";
                case "43":
                    return "[视频]";
                case "62":
                    return "[小视频]";
                case "53":
                    return "[对方语音或视频呼叫你]";
                case "10000": // 包含红包消息
                    return msg;
                case "42":
                    return $"她向你推荐了名片: [{WXService.DecodeMsgFace(Regex.Match(msg, "nickname=\"(.*)\"").Groups[1].Value)}]";
                case "10002":
                    return "[对方撤回了一条消息]";
            }

            return msg;
        }

        // 处理图片包括用户自己收藏的图片
        // 处理type为3和47的情况
        // byte[]为小图片数据，string为大图地址
        // 为null的时候表示无更大的图
        public virtual Tuple<byte[], string> OnHandlePic(string msgType, string MsgID, string skey)
        {
            var type = msgType == "3" ? "slave" : "big";
            var url = $"https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxgetmsgimg?&MsgID={MsgID}&skey={skey}";
            var bytes = BaseService.SendGetRequest($"{url}&type={type}");

            return new Tuple<byte[], string>(bytes, msgType == "3" ? url : null);
        }

        // 处理音频
        // 暂不开启
        public virtual byte[] OnHandleAudio(string msgid, string skey)
        {
            var url = $"https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxgetvoice?msgid={msgid}&skey={skey}";
            return BaseService.SendGetRequest(url);
        }

        // 处理视频
        // 暂不开启
        public virtual byte[] OnHandleVideo(string msgid, string skey)
        {
            var url = $"https://wx.qq.com/cgi-bin/mmwebwx-bin/webwxgetvideo?msgid={msgid}&skey={skey}";
            return BaseService.SendGetRequest(url);
        }
    }
}
