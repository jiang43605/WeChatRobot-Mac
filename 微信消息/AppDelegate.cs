using AppKit;
using Foundation;
using WXLogin;
using System.IO;
using System.Linq;

namespace 微信消息
{
    [Register("AppDelegate")]
    public class AppDelegate : NSApplicationDelegate
    {
        public AppDelegate()
        {
        }

        public override void DidFinishLaunching(NSNotification notification)
        {
            // Insert code here to initialize your application
        }

        public override void WillTerminate(NSNotification notification)
        {
            // Config.PICSAVEDIRECTORY一定存在，在程序初始化时就已经创建
            foreach (var item in Directory.GetFiles(Config.PICSAVEDIRECTORY)) File.Delete(item);

            try
            {
                WXService.LoginOut();
            }
            catch
            {
                // 当用户未登录时候的退出
            }
        }

        [Export("applicationShouldTerminateAfterLastWindowClosed:")]
        public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender)
        {
            return true;
        }

        public void OpenMainWindow()
        {
            var storyboard = NSStoryboard.FromName("Main", null);
            var controller = storyboard.InstantiateControllerWithIdentifier("MainWindow") as NSWindowController;
            controller.ShowWindow(this);
        }
    }
}
