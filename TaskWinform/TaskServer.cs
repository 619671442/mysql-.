using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FluentScheduler;
using Newtonsoft.Json;

namespace TaskWinform
{
    public class TaskServer
    {
        private static TaskServer _taskTest;

        public static TaskServer Instance
        {
            get
            {
                if (_taskTest == null)
                {
                    _taskTest = new TaskServer();
                }
                return _taskTest;
            }
        }

        public void Init()
        {
            Registry reg = new Registry();
            Dictionary<TaskModel, Action> dic = GetTaskList();
            if (dic.Any())
            {
                foreach (var item in dic)
                {
                    DateTime excuteTime;
                    if (string.IsNullOrWhiteSpace(item.Key.ExcuteTime))
                    {
                        excuteTime = DateTime.Now;
                    }
                    else
                    {
                        if (DateTime.TryParse(item.Key.ExcuteTime, out excuteTime))
                        {
                            excuteTime = DateTime.Now;
                        }
                    }

                    switch (item.Key.Type)
                    {
                        case TaskExccuteType.OnceTime:
                            reg.Schedule(() => item.Value()).ToRunOnceAt(excuteTime);
                            break;
                        case TaskExccuteType.IntervalSecond:

                            int seconds;
                            if (int.TryParse(item.Key.IntervalTime, out seconds))
                            {
                                reg.Schedule(() => item.Value()).ToRunOnceAt(excuteTime).AndEvery(seconds).Seconds();
                            }
                            break;
                        case TaskExccuteType.IntervalHour:
                            int hour;
                            if (int.TryParse(item.Key.IntervalTime, out hour))
                            {
                                reg.Schedule(() => item.Value()).ToRunOnceAt(excuteTime).AndEvery(hour).Hours();
                            }
                            break;
                        case TaskExccuteType.IntervalDay:
                            int day;
                            if (int.TryParse(item.Key.IntervalTime, out day))
                            {
                                reg.Schedule(() => item.Value()).ToRunOnceAt(excuteTime).AndEvery(day).Days();
                            }
                            break;
                        case TaskExccuteType.SpecifyTime:
                            int specifyDay;
                            if (!int.TryParse(item.Key.IntervalTime, out specifyDay))
                            {
                                specifyDay = 1;
                            }
                            DateTime time = Convert.ToDateTime(item.Key.ExcuteTime);
                            reg.Schedule(() => item.Value()).ToRunEvery(specifyDay).Days().At(time.Hour, time.Minute);
                            break;
                    }
                }
            }

            JobManager.Initialize(reg);
        }

        public void Stop()
        {
            JobManager.Stop();
        }

        private Dictionary<TaskModel, Action> GetTaskList()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "Task.json";
            string result = File.ReadAllText(path);
            var list = JsonConvert.DeserializeObject<List<TaskModel>>(result);
            Dictionary<TaskModel, Action> dic = new Dictionary<TaskModel, Action>();
            if (list != null && list.Any())
            {
                foreach (var taskModel in list)
                {
                    Assembly assembly = Assembly.LoadFrom(AppDomain.CurrentDomain.BaseDirectory + taskModel.AssemblyPath);
                    Type type = assembly.GetType(taskModel.Namespace);
                    MethodInfo methodInfo = type.GetMethod(taskModel.Method, BindingFlags.Instance | BindingFlags.Public);
                    Action action = () => InvokeMethod(methodInfo, type, taskModel.Parameters);
                    dic.Add(taskModel, action);
                }
            }
            return dic;
        }

        /// <summary>
        /// 执行具体任务操作
        /// </summary>
        /// <param name="methodInfo"></param>
        /// <param name="type"></param>
        private void InvokeMethod(MethodInfo methodInfo, Type type, object parameters)
        {
            object obj = Activator.CreateInstance(type);
            if (methodInfo != null)
            {
                object result = methodInfo.Invoke(obj, new object[] { parameters });

            }
        }
    }
}
