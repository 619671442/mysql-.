using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using IdentityModel;
using Microsoft.IdentityModel.Tokens;
using System.Xml.Linq;
using System.Data.Linq.Mapping;
using SqlSugar;
using System.Data.SqlClient;
using System.Data;
using MySql.Data.MySqlClient;
using System.Text.RegularExpressions;

namespace TaskExecute
{
    public class CheckAsync
    {
       

        public String Execute(Dictionary<String, String> parameters)
        {
            //string error_str = "Could not execute Write_rows event on table test.test_table; Duplicate entry '4' for key 'PRIMARY', Error_code: 1062; handler error HA_ERR_FOUND_DUPP_KEY; the event's master log master-bin.000002, end_log_pos 1185 ";
            //int IndexofAA = error_str.IndexOf("Duplicate entry '");
            //int IndexofBB = error_str.IndexOf("' for key 'PRIMARY'");
            //string pk_str = error_str.Substring(IndexofAA + "Duplicate entry '".Length, IndexofBB - IndexofAA - "Duplicate entry '".Length);


            LogHelper.WriteTaskLog("-----------------------------------------------------------------------------------------------------------\r\n", "TaskLog.txt");
            //String log = ;
            DateTime startDateTime = DateTime.Now;
            LogHelper.WriteTaskLog("定时任务执行，开始时间：" + startDateTime + "\r\n", "TaskLog.txt");
            
            MysqlConnector mc = new MysqlConnector();
            mc.SetServer(parameters["Server"])
            .SetDataBase(parameters["DataBase"])
            .SetUserID(parameters["UserID"])
            .SetPassword(parameters["Password"])
            .SetPort(parameters["Port"])
            .SetCharset(parameters["Charset"]);

            //执行查询
            MySqlDataReader reader = mc.ExeQuery("show slave status;");
            if (!reader.HasRows)
            {
                LogHelper.WriteTaskLog("未查询到slave状态，从库未设置\r\n", "TaskLog.txt");
             }
            else
            {
                Dictionary<string, object> slave_status = new Dictionary<string, object>();
                while (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        slave_status.Add(reader.GetName(i), reader.GetValue(i));
                    }
                }
                if (string.Equals("Yes", slave_status["Slave_IO_Running"])&& string.Equals("Yes", slave_status["Slave_SQL_Running"]))
                {
                    //从库同步未进行
                    //展示停止原因
                    LogHelper.WriteTaskLog("同步任务执行正常！\r\n", "TaskLog.txt");
                    
                }else if (string.IsNullOrEmpty(slave_status["Slave_IO_State"]+""))
                {
                    LogHelper.WriteTaskLog("Slave_IO_State状态未空,同步任务未开始,请手动执行start slave;！\r\n", "TaskLog.txt");
                    
                }
                else
                {
                    //未正常执行，处理异常
                    HandleExceptions( slave_status,mc);
                }
             }


            


            

            
            LogHelper.WriteTaskLog("定时任务执行完毕，开始时间：" + startDateTime + "结束时间：" + DateTime.Now + "\r\n", "TaskLog.txt");

            return "执行完毕";
        }
        //未正常执行，处理异常
        private void HandleExceptions( Dictionary<string, object> slave_status, MysqlConnector mc)
        {
            LogHelper.WriteTaskLog("同步任务执行错误！原因：\r\n", "TaskLog.txt");            
            LogHelper.WriteTaskLog("Last_IO_Errno" + slave_status["Last_IO_Errno"] + "\r\n", "TaskLog.txt");
            LogHelper.WriteTaskLog("Last_IO_Error" + slave_status["Last_IO_Error"] + "\r\n", "TaskLog.txt");
            LogHelper.WriteTaskLog("Last_SQL_Errno" + slave_status["Last_SQL_Errno"] + "\r\n", "TaskLog.txt");
            LogHelper.WriteTaskLog("Last_SQL_Error" + slave_status["Last_SQL_Error"] + "\r\n", "TaskLog.txt");
            //判断原因是不是sql主键冲突,如果是主键冲突,自动解决
            //主键冲突例子Could not execute Write_rows event on table test.test_table; Duplicate entry '4' for key 'PRIMARY', Error_code: 1062; handler error HA_ERR_FOUND_DUPP_KEY; the event's master log master-bin.000002, end_log_pos 1185 
            string sql_error_str = slave_status["Last_SQL_Error"] + "";
            //error_str = "Could not execute Write_rows event on table test.test_table; Duplicate entry '4' for key 'PRIMARY', Error_code: 1062; handler error HA_ERR_FOUND_DUPP_KEY; the event's master log master-bin.000002, end_log_pos 1185 ";
            if (sql_error_str.Contains("PRIMARY"))
            {
                //处理PRIMARY异常
                HandlePkExceptions(sql_error_str, mc);
            }
        }
        //处理PRIMARY异常
        private void HandlePkExceptions(string error_str, MysqlConnector mc)
        {
            LogHelper.WriteTaskLog("错误语句中包含PRIMARY,判定为主键冲突,开始自动处理PRIMARY冲突:\r\n", "TaskLog.txt");
            
            int IndexofA = error_str.IndexOf("on table ");
            int IndexofB = error_str.IndexOf("; ");
            string database_table = error_str.Substring(IndexofA + "on table ".Length, IndexofB - IndexofA - "on table ".Length);

            //获取数据库名称
            string TABLE_SCHEMA = database_table.Split('.')[0].Trim();
            //获取表名
            string TABLE_NAME = database_table.Split('.')[1].Trim();
            LogHelper.WriteTaskLog("从错误信息获取到冲突的表名为:" + TABLE_SCHEMA + ">" + TABLE_NAME + "\r\n", "TaskLog.txt");



            int IndexofAA = error_str.IndexOf("Duplicate entry '");
            int IndexofBB = error_str.IndexOf("' for key 'PRIMARY'");
            string pk_str = error_str.Substring(IndexofAA + "Duplicate entry '".Length, IndexofBB - IndexofAA - "Duplicate entry '".Length);

            //获取主键值
            string PK_VALUE = pk_str;
            LogHelper.WriteTaskLog("从错误信息获取到冲突的键值为:" + PK_VALUE + "\r\n", "TaskLog.txt");
            
            //查询该冲突的表的主键字段名                            
            string PK_COLUMN_NAME = GetPkColumnName(mc, TABLE_NAME, TABLE_SCHEMA);
            LogHelper.WriteTaskLog("从数据库获取到冲突的表名的主键列名为:" + PK_COLUMN_NAME + "\r\n", "TaskLog.txt");
            
            //先查从库是否包含指定数据
            string sql_query_data = "select * from " + database_table + "  where " + PK_COLUMN_NAME + "='" + PK_VALUE + "';";
            MySqlDataReader reader2 = mc.ExeQuery(sql_query_data);
            if (!reader2.HasRows)
            {
                LogHelper.WriteTaskLog("从库中未查询到主键冲突的数据" + sql_query_data + "\r\n", "TaskLog.txt");
            }
            else
            {
                LogHelper.WriteTaskLog("查询到冲突的数据:\r\n", "TaskLog.txt");
                string chongtu_data = "";
                while (reader2.Read())
                {
                    for (int i = 0; i < reader2.FieldCount; i++)
                    {
                        chongtu_data += reader2.GetName(i) + ":" + reader2.GetValue(i) + ";";
                    }
                }
                LogHelper.WriteTaskLog(chongtu_data + "\r\n", "TaskLog.txt");
                //删除数据
                string sql_delete = "delete from " + database_table + "  where " + PK_COLUMN_NAME + "='" + PK_VALUE + "';";
                MySqlDataReader reader_delete = mc.ExeQuery(sql_delete);
                LogHelper.WriteTaskLog("已删除冲突数据\r\n", "TaskLog.txt");
                
                //继续执行同步
                string sql_start_slave = "start slave";
                MySqlDataReader reader_start_slave = mc.ExeQuery(sql_start_slave);
                LogHelper.WriteTaskLog("PRIMARY冲突处理完毕", "TaskLog.txt");
            }
         }

        //获取主键列名
        private string GetPkColumnName(MysqlConnector mc,string TABLE_NAME,string TABLE_SCHEMA)
        {
            string sql_query_pk = "SELECT TABLE_NAME,COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE TABLE_NAME<> 'dtproperties' and TABLE_NAME = '" + TABLE_NAME + "' AND table_schema = '" + TABLE_SCHEMA + "' ;";
            MySqlDataReader reader = mc.ExeQuery(sql_query_pk);
            Dictionary<string, object> k_v = new Dictionary<string, object>();
            while (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    k_v.Add(reader.GetName(i), reader.GetValue(i));
                }
            }
            return k_v["COLUMN_NAME"].ToString();
        }
    }

    public class LogHelper
    {
        public static void WriteTaskLog(string text, string fileName)
        {
            string _filePath = AppDomain.CurrentDomain.BaseDirectory + fileName;
            FileStream fs = new FileStream(_filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            StreamWriter m_streamWriter = new StreamWriter(fs);
            m_streamWriter.BaseStream.Seek(0, SeekOrigin.End);
            m_streamWriter.WriteLine(DateTime.Now + " " + text);
            m_streamWriter.Flush();
            m_streamWriter.Close();
            fs.Close();
        }
    }
    public class MysqlConnector
    {
        string server = null;
        string userid = null;
        string password = null;
        string database = null;
        string port = "3306";
        string charset = "utf-8";

        public MysqlConnector() { }
        public MysqlConnector SetServer(string server)
        {
            this.server = server;
            return this;
        }

        public MysqlConnector SetUserID(string userid)
        {
            this.userid = userid;
            return this;
        }

        public MysqlConnector SetDataBase(string database)
        {
            this.database = database;
            return this;
        }

        public MysqlConnector SetPassword(string password)
        {
            this.password = password;
            return this;
        }
        public MysqlConnector SetPort(string port)
        {
            this.port = port;
            return this;
        }
        public MysqlConnector SetCharset(string charset)
        {
            this.charset = charset;
            return this;
        }



        #region  建立MySql数据库连接
        /// <summary>
        /// 建立数据库连接.
        /// </summary>
        /// <returns>返回MySqlConnection对象</returns>
        private MySqlConnection GetMysqlConnection()
        {
            string M_str_sqlcon = string.Format("server={0};user id={1};password={2};database={3};port={4};Charset={5}", server, userid, password, database, port, charset);
            MySqlConnection myCon = new MySqlConnection(M_str_sqlcon);
            return myCon;
        }
        #endregion

        #region  执行MySqlCommand命令
        /// <summary>
        /// 执行MySqlCommand
        /// </summary>
        /// <param name="M_str_sqlstr">SQL语句</param>
        public void ExeUpdate(string M_str_sqlstr)
        {
            MySqlConnection mysqlcon = this.GetMysqlConnection();
            mysqlcon.Open();
            MySqlCommand mysqlcom = new MySqlCommand(M_str_sqlstr, mysqlcon);
            mysqlcom.ExecuteNonQuery();
            mysqlcom.Dispose();
            mysqlcon.Close();
            mysqlcon.Dispose();
        }
        #endregion

        #region  创建MySqlDataReader对象
        /// <summary>
        /// 创建一个MySqlDataReader对象
        /// </summary>
        /// <param name="M_str_sqlstr">SQL语句</param>
        /// <returns>返回MySqlDataReader对象</returns>
        public MySqlDataReader ExeQuery(string M_str_sqlstr)
        {
            Console.WriteLine(M_str_sqlstr);
            MySqlConnection mysqlcon = this.GetMysqlConnection();
            MySqlCommand mysqlcom = new MySqlCommand(M_str_sqlstr, mysqlcon);
            mysqlcon.Open();
            MySqlDataReader mysqlread = mysqlcom.ExecuteReader(CommandBehavior.CloseConnection);
            return mysqlread;
        }
        #endregion
    }


}
