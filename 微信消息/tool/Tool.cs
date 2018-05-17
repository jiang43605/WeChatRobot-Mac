using AppKit;
using Foundation;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace 微信消息
{
    public static class Tool
    {
        private static readonly NSUserNotificationCenter _defaultUserNotificationCenter;

        static Tool()
        {
            _defaultUserNotificationCenter = NSUserNotificationCenter.DefaultUserNotificationCenter;
            _defaultUserNotificationCenter.Delegate = new NotificationDelegate();
        }

        /// <summary>
        /// 发送通知消息
        /// </summary>
        /// <param name="text">Text.</param>
        public static void SendNotification(string title, string text, NSDictionary userInfo = null)
        {
            //nSUserNotification
            //nSUserNotification.Subtitle = "副标题";
            //nSUserNotification.HasActionButton = true;
            //nSUserNotification.ActionButtonTitle = "确定";
            //nSUserNotification.OtherButtonTitle = "取消";

            NSUserNotification nSUserNotification = new NSUserNotification();
            nSUserNotification.Title = title;
            nSUserNotification.InformativeText = text;
            nSUserNotification.SoundName = "NSUserNotificationDefaultSoundName";
            nSUserNotification.HasReplyButton = true;
            nSUserNotification.ResponsePlaceholder = "回复消息";

            if (userInfo != null) nSUserNotification.UserInfo = userInfo;

            _defaultUserNotificationCenter.DeliverNotification(nSUserNotification);
        }
    }
}
