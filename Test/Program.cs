using System;
using WXLogin;
using System.IO;

namespace Test
{
    class Program
    {

        private static string preMsg;
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
                    MsgHandle(msg);
                }
            });
        }


        static void MsgHandle(WXMsg msg)
        {
            var time = msg.Time.ToString("HH:mm:ss");
            var mt = System.Text.RegularExpressions.Regex.Match(msg.FromNickName, @"^\[(.*)\](.*)$");
            var eqNickName = msg.FromNickName;
            if (mt.Success) eqNickName = mt.Groups[1].Value;

            if (!eqNickName.Equals(preMsg) && !eqNickName.Equals("Chengf"))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(eqNickName + "-" + time);
                Console.ResetColor();
            }

            var name = eqNickName.Equals("Chengf") ? "我" : eqNickName;
            if (mt.Success)
            {
                name = eqNickName.Equals("Chengf") ? "我" : mt.Groups[2].Value;
            }

            Console.WriteLine($"[{name}][{time}]{msg.Msg}");

            if (!eqNickName.Equals("Chengf")) preMsg = eqNickName;
        }
    }
}
