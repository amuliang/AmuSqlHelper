using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmuTools
{
    /*
        // 示例
        TimerHelper.Run();
        TimerHelper.Add(CreateAction(), 0, 1000, 2); // 假定CreateAction返回一个Action对象
        TimerHelper.Add(CreateAction(), 500, 1000, 4);
    */
    public class TimerUnit
    {
        public long BeginTime { get; set; }
        public int Period { get; set; }
        public int Times { get; set; }
        public int ExecTimes { get; set; }
        public Action<TimerUnit> Action { get; set; }
    }

    public class TimerHelper
    {
        static public LinkedList<TimerUnit> list = new LinkedList<TimerUnit>();
        static private Thread thread1 = new Thread(new ThreadStart(Thread1));
        //static private bool pause = true;

        /// <summary>
        /// 添加新的定时器
        /// </summary>
        /// <param name="action">执行的动作</param>
        /// <param name="beginTime">时间戳，如果小于1000000000000，则默认当前时间的时间戳加上参数值</param>
        /// <param name="period">每次循环的时间间隔</param>
        /// <param name="times">循环多少次，默认为1,-1表示无限循环</param>
        static public void Add(Action<TimerUnit> action, long beginTime = 0, int period = 0, int times = 1)
        {
            if (beginTime < 100000000000)
            {
                beginTime += long.Parse(DateTimeEx.GetNowTimeStamp());
            }
            list.AddLast(new TimerUnit { Action = action, BeginTime = beginTime, Period = period, Times = times, ExecTimes = 0 });
        }

        static public void Add(Action<TimerUnit> action, DateTime datetime, int period = 0, int times = 1)
        {
            Add(action, long.Parse(DateTimeEx.DateTimeToTimeStamp(datetime)), period, times);
        }

        static public void Add(Action<TimerUnit> action, TimeSpan timeSpan, int period = 0, int times = 1)
        {
            Add(action, long.Parse(DateTimeEx.DateTimeToTimeStamp(DateTime.Now + timeSpan)), period, times);
        }

        static public void Add(Action<TimerUnit> action, long beginTime, TimeSpan period, int times = 1)
        {
            Add(action, beginTime, (int)period.Ticks / 10000, times);
        }

        static public void Add(Action<TimerUnit> action, DateTime datetime, TimeSpan period, int times = 1)
        {
            Add(action, datetime, (int)period.Ticks / 10000, times);
        }

        static public void Add(Action<TimerUnit> action, TimeSpan timeSpan, TimeSpan period, int times = 1)
        {
            Add(action, timeSpan, (int)period.Ticks / 10000, times);
        }

        static public void Run()
        {
            thread1.Start();
        }

        private static void Thread1()
        {
            while (true)
            {
                long now = long.Parse(DateTimeEx.GetNowTimeStamp());
                LinkedListNode<TimerUnit> node = list.First;
                while(node != null)
                {
                    LinkedListNode<TimerUnit> temp = node;
                    node = node.Next;
                    TimerUnit tu = temp.Value;
                    int times = tu.Times - tu.ExecTimes;
                    if (times == 0)
                    {
                        list.Remove(temp);
                        continue;
                    }
                    if (tu.BeginTime + tu.ExecTimes * tu.Period < now)
                    {
                        tu.Action(tu);
                        tu.ExecTimes += 1;
                    }
                }
            }
        }
    }

    static class DateTimeEx
    {
        // 将时间转为时间戳
        public static string DateTimeToTimeStamp(System.DateTime time)
        {
            System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1, 0, 0, 0, 0));
            long t = (time.Ticks - startTime.Ticks) / 10000;   //除10000调整为13位      
            return t.ToString();
        }
        public static string GetNowTimeStamp()
        {
            return DateTimeToTimeStamp(DateTime.Now);
        }
    }

}
