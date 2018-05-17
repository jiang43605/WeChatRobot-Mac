using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace WXLogin
{
    /// <summary>
    /// 微信消息
    /// </summary>
    public class WXMsg
    {

        private string _fromMemberUserName;
        private string _toMemberUserName;
        private string _fromNickName;
        private string _toNickName;
        private static readonly string UNKOWNNAME = "[unkown]";

        /// <summary>
        /// 消息发送方
        /// </summary>
        public string From
        {
            get;
            internal set;
        }
        /// <summary>
        /// 消息发送方昵称
        /// 消息经过格式
        /// 如果是群组消息
        /// 形式为：[群组名]群人员名
        /// 如果要获取群的名称，请通过FromUserInfo
        /// </summary>
        public string FromNickName
        {
            get
            {
                if (this._fromNickName != null) return this._fromNickName;
                if (this.FromUserInfo == null) return UNKOWNNAME;

                if (FromUserInfo.UserType == UserType.ChatRoom)
                    this._fromNickName = $"[{this.FromUserInfo.ShowName}]{this.FromMemberUserName ?? UNKOWNNAME}";
                else
                    this._fromNickName = this.FromUserInfo.ShowName;

                return this._fromNickName;
            }
        }
        /// <summary>
        /// 消息接收方
        /// </summary>
        public string To
        {
            internal set;
            get;
        }
        /// <summary>
        /// 消息接收方昵称
        /// 消息经过格式
        /// 如果是群组消息
        /// 形式为：[群组名]群人员名
        /// 如果要获取群的名称，请通过ToUserInfo
        /// </summary>
        public string ToNickName
        {
            get
            {
                if (this._toNickName != null) return this._toNickName;
                if (this.ToUserInfo == null) return UNKOWNNAME;

                if (ToUserInfo.UserType == UserType.ChatRoom)
                    this._toNickName = $"[{this.ToUserInfo.ShowName}]{this.ToMemberUserName ?? UNKOWNNAME}";
                else
                    this._toNickName = this.ToUserInfo.ShowName;

                return this._toNickName;
            }
        }
        /// <summary>
        /// 消息发送时间
        /// </summary>
        public DateTime Time
        {
            get;
            internal set;
        }
        /// <summary>
        /// 是否已读
        /// </summary>
        public bool Readed { get; internal set; }

        /// <summary>
        /// 消息内容,经过一定处理后的文本消息
        /// </summary>
        public string Msg
        {
            get;
            internal set;
        }
        /// <summary>
        /// 原始消息内容
        /// </summary>
        /// <value>The origin message.</value>
        public string OriginMsg { internal set; get; }
        /// <summary>
        /// 图片消息
        /// </summary>
        /// <value>The audio message.</value>
        public byte[] PicMsg
        {
            get;
            internal set;
        }
        /// <summary>
        /// 消息类型
        /// </summary>
        public int Type
        {
            get;
            internal set;
        }

        public WXUser FromUserInfo
        {
            internal set;
            get;
        }

        public WXUser ToUserInfo
        {
            internal set;
            get;
        }

        /// <summary>
        /// 如果当前消息来源是群组
        /// 则通过此属性可以得到发送方群成员名称
        /// </summary>
        /// <value>The name of the get member user.</value>
        public string FromMemberUserName
        {
            get
            {
                if (this._fromMemberUserName != null) return this._fromMemberUserName;
                if (this.FromUserInfo == null || this.FromUserInfo.UserType != UserType.ChatRoom) return null;

                var splitString = this.OriginMsg.Split(new[] { ":<br/>" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                this._fromMemberUserName = this.FromUserInfo.MemberList.SingleOrDefault(o => o.UserName == splitString)?.ShowName;

                return this._fromMemberUserName;
            }
        }

        /// <summary>
        /// 如果当前消息来源是群组
        /// 则通过此属性可以得到接收方群成员名称
        /// </summary>
        /// <value>The name of the to member user.</value>
        public string ToMemberUserName
        {
            get
            {
                if (this._toMemberUserName != null) return this._toMemberUserName;
                if (this.ToUserInfo == null || this.ToUserInfo.UserType != UserType.ChatRoom) return null;

                var splitString = this.OriginMsg.Split(new[] { ":<br/>" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                this._toMemberUserName = this.ToUserInfo.MemberList.SingleOrDefault(o => o.UserName == splitString)?.ShowName;

                return this._toMemberUserName;
            }
        }
    }

    /// <summary>
    /// interface for handle wx msg
    /// </summary>
    public interface IWXMsgHandle
    {
        object Handle(JToken msgInfo);
    }
}
