// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace 微信消息
{
    [Register("ViewController")]
    partial class ViewController
    {
        [Outlet]
        AppKit.NSTextField LabelInputDisplay { get; set; }

        [Outlet]
        AppKit.NSTextField LoginInfo { get; set; }

        [Outlet]
        AppKit.NSTextView TextPrint { get; set; }

        [Action("ButtonSend:")]
        partial void ButtonSend(Foundation.NSObject sender);

        [Action("InputChange:")]
        partial void InputChange(Foundation.NSObject sender);

        void ReleaseDesignerOutlets()
        {
            if (LabelInputDisplay != null)
            {
                LabelInputDisplay.Dispose();
                LabelInputDisplay = null;
            }

            if (LoginInfo != null)
            {
                LoginInfo.Dispose();
                LoginInfo = null;
            }

            if (TextPrint != null)
            {
                TextPrint.Dispose();
                TextPrint = null;
            }
        }
    }
}
