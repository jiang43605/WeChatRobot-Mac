using System;
using AppKit;
using CoreGraphics;
using Foundation;
using WXLogin;
using System.Net;
using System.IO;

namespace 微信消息
{
    public class TextViewDelegate : NSTextViewDelegate
    {
        [Export("textView:clickedOnCell:inRect:atIndex:")]
        public override void CellClicked(NSTextView textView, NSTextAttachmentCell cell, CGRect cellFrame, nuint charIndex)
        {
            var textCell = cell as ImageCell;
            if (textCell == null) return;

            // 存在就直接打开
            var localFileName = textCell.GetData("localName");
            if (localFileName != null)
            {
                OpenPreview(localFileName);
                return;
            }

            // 开始创建
            var url = textCell.GetData("picUrl");
            if (url == null) return;
            var randomPicName = Config.PICSAVEDIRECTORY + Guid.NewGuid().ToString();

            // get pic from network
            File.WriteAllBytes(randomPicName, BaseService.SendGetRequest(url));
            var fullPathName = Path.GetFullPath(randomPicName);
            textCell.SetData("localName", fullPathName);
            OpenPreview(fullPathName);
        }

        /// <summary>
        /// url为绝对地址
        /// </summary>
        /// <param name="url">URL.</param>
        private void OpenPreview(string url)
        {
            NSWorkspace.SharedWorkspace.OpenFile(url);
        }
    }
}
