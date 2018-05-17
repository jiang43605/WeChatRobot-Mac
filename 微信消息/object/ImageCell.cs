using AppKit;
using Foundation;
using System.Collections.Generic;

namespace 微信消息
{
    public class ImageCell : NSTextAttachmentCell
    {
        private readonly Dictionary<string, string> _data;

        public ImageCell(NSImage image) : base(image)
        {
            _data = new Dictionary<string, string>();
        }

        public void SetData(string key, string value)
        {
            _data.Add(key, value);
        }

        public string GetData(string key)
        {
            if (!_data.ContainsKey(key)) return null;
            return _data[key];
        }
    }
}
