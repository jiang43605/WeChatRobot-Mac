using System;
using AppKit;
using Foundation;
using WXLogin;
using System.Linq;

namespace 微信消息
{
    public class NotificationDelegate : NSUserNotificationCenterDelegate
    {
        public override bool ShouldPresentNotification(NSUserNotificationCenter center, NSUserNotification notification)
        {
            return true;
        }

        [Export("userNotificationCenter:didActivateNotification:")]
        public override void DidActivateNotification(NSUserNotificationCenter center, NSUserNotification notification)
        {
            if (notification.ActivationType == NSUserNotificationActivationType.ContentsClicked)
            {
                var mainWindow = NSApplication.SharedApplication.Windows.FirstOrDefault(o => o.Identifier == "MainWindow");
                if (mainWindow != null) mainWindow.IsVisible = true;
            }

            else if (notification.ActivationType == NSUserNotificationActivationType.Replied)
            {
                var response = notification.Response.Value;
                var nickName = notification.Title;
                var userName = notification.UserInfo?["userName"]?.ToString();

                if (userName == null) return;
                if (string.IsNullOrWhiteSpace(response)) return;
                if (string.IsNullOrWhiteSpace(nickName)) return;

                var wxService = WXService.Instance;
                wxService.SendMsgAsync(response, wxService.Me.UserName, userName, 1);
            }
        }
    }
}
