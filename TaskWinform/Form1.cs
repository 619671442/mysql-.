using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace TaskWinform
{
    public partial class Form1 : Form
    {
        private bool _isStarted;
        private System.Timers.Timer timer;
        private string path = AppDomain.CurrentDomain.BaseDirectory + "Task.json";

        //private string request_token_url;

        public Form1()
        {
            InitializeComponent();
            System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = false;
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            //FileWatche();
            //this.textBox1.BringToFront();

        }

        

        private void Form1_Load(object sender, System.EventArgs e)
        {
            FileSystemWatcher fsw = new FileSystemWatcher();
            fsw.Path = AppDomain.CurrentDomain.BaseDirectory;
            fsw.Filter = "TaskLog.txt";
            fsw.NotifyFilter = System.IO.NotifyFilters.LastWrite;
            fsw.Changed += new FileSystemEventHandler(FileChange);
            fsw.EnableRaisingEvents = true;
        }


        private void FileChange(object sender, FileSystemEventArgs e)
        {
            FileStream fs = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "TaskLog.txt", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
            textBox1.Text = sr.ReadToEnd();
            sr.Close();
        }


        private void btnStart_Click(object sender, EventArgs e)
        {
            if (_isStarted)
            {
                this.textBox1.Text = this.textBox1.Text + "定时任务已经执行\r\n";
            }
            else
            {
                TaskServer.Instance.Init();
                _isStarted = true;
                btnStart.Enabled = false;
                btnStop.Enabled = true;
                this.textBox1.Text = this.textBox1.Text + "定时任务开始执行。。。\r\n";
            }
        }
        private void updateTextBox1(String log)
        {
            this.textBox1.Text = this.textBox1.Text + log + "\r\n";
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (_isStarted)
            {
                TaskServer.Instance.Stop();
                _isStarted = false;
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                this.textBox1.Text = this.textBox1.Text + "定时任务停止。。。\r\n";
            }
            else
            {
                this.textBox1.Text = this.textBox1.Text + "定时任务已经停止。\r\n";
            }
        }

 
        private void OnCreatedFile(object sender, FileSystemEventArgs e)
        {
            string FilePath = e.FullPath;//文件地址
            string FileName = e.Name;//文件名

            (sender as FileSystemWatcher).EnableRaisingEvents = false;

            //設置讀取時間，当监测文件改变等待指定的时间才开始读取
            setInternal(FileName, FilePath);

            (sender as FileSystemWatcher).EnableRaisingEvents = true;//这样可以保证changed事件可以被重新触发。
            updateTextBox1("日志文件TaskLog.txt被创建");
        }
        private void OnChangedFile(object sender, FileSystemEventArgs e)
        {
            string FilePath = e.FullPath;//文件地址
            string FileName = e.Name;//文件名

            (sender as FileSystemWatcher).EnableRaisingEvents = false;

            //設置讀取時間，当监测文件改变等待指定的时间才开始读取
            setInternal(FileName, FilePath);

            (sender as FileSystemWatcher).EnableRaisingEvents = true;//这样可以保证changed事件可以被重新触发。


            updateTextBox1(GetLastLine(AppDomain.CurrentDomain.BaseDirectory + "TaskLog.txt"));
        }
        public void setInternal(string name, string path)
        {
            this.timer = new System.Timers.Timer(10000);//实例化Timer类，设置时间间隔   
            this.timer.Elapsed += new System.Timers.ElapsedEventHandler((s, e) => copy(s, e, name, path));//当到达时间的时候执行事件 
            this.timer.AutoReset = false;//false是执行一次，true是一直执行,当为true时会导致只监测一个文档
            this.timer.Enabled = true;//设置是否执行System.Timers.Timer.Elapsed事件 
        }

        public void copy(object source, ElapsedEventArgs e, string name1, string name2)
        {

            //需要执行的操作 
            Console.WriteLine("copy");
        }

        /// <summary>
        /// 提取文本最后一行数据
        /// </summary>
        /// <param name="fs">文件流</param>
        /// <returns>最后一行数据</returns>
        private string GetLastLine(string path)
        {
            string lastestLine = "";
            try
            {
                string[] allLines = System.IO.File.ReadAllLines(path);
                lastestLine = allLines[allLines.Length - 1];
            }
            catch
            {
                return "获取日志错误!";
            }
            return lastestLine;
        }



        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                //还原窗体显示
                WindowState = FormWindowState.Normal;
                //激活窗体并给予它焦点
                this.Activate();
                //任务栏区显示图标
                this.ShowInTaskbar = true;
                //托盘区图标隐藏
                notifyIcon1.Visible = false;
            }
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)  //判断是否最小化
            {
                this.ShowInTaskbar = false;  //不显示在系统任务栏
                notifyIcon1.Visible = true;  //托盘图标可见
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            this.textBox1.Select(this.textBox1.TextLength, 0);
            this.textBox1.ScrollToCaret();
        }
 

        
    }
}
