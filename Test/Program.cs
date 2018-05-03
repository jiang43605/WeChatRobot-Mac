using System;
using WXLogin;
using System.IO;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {

           var ls = new LoginService();

            var codeStream = ls.GetQRCode();
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "WX二维码.png");
            using (var fs = new FileStream(path, FileMode.Create))
            {
                fs.Write(codeStream, 0, codeStream.Length);
            }


            while (true)
            {
                var loginResult = ls.LoginCheck();

                if (loginResult is Stream)
                {
                    //已扫描 未登录
                    Console.WriteLine("please click login btton in you phone!");
                }
                else if (loginResult is string)
                {
                    //已完成登录
                    ls.GetSidUid(loginResult as string);
                    break;
                }
            }

            var wxService = WXService.Instance;
            wxService.InitData();

            var allFriend = wxService.AllContactCache;

            wxService.Listening(msgs =>
            {
                foreach (var msg in msgs)
                {
                    //Console.WriteLine(msg);
                    Console.WriteLine($"来自：{msg.FromNickName}; type：{msg.Type}");
                    Console.WriteLine($"{msg.Msg}");
                    // do you logic
                }
            });
        }
    }
}
