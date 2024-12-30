using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOAP_SERVER
{
    public class DBconn
    {
        //커넥션 객체

        private static SqlConnection conn = null;
        public static string DBConnString { get; private set; }
        public static bool bDBConnCheck = false;
        private static int errorBoxCount = 0;

        /// <summary>

        /// 생성자

        /// </summary>

        public DBconn() { }



        public static SqlConnection DBConn

        {

            get

            {

                if (!ConnectToDB())

                {

                    return null;

                }

                return conn;

            }

        }



        /// <summary>

        /// Database 접속 시도

        /// </summary>

        /// <returns></returns>

        public static bool ConnectToDB()

        {

            if (conn == null)

            {
                //서버명, 초기 DB명, 인증 방법
                DBConnString = @"Server=222.231.58.71; database=SMS; uid=eshinan; pwd=!eshinan4600; TrustServerCertificate=True";
                conn = new SqlConnection(DBConnString);
            }



            try

            {

                if (!IsDBConnected)

                {

                    conn.Open();



                    if (conn.State == System.Data.ConnectionState.Open)

                    {

                        bDBConnCheck = true;

                    }

                    else

                    {

                        bDBConnCheck = false;

                    }

                }

            }

            catch (SqlException e)

            {

                errorBoxCount++;

                if (errorBoxCount == 1)

                {

                    Console.WriteLine(e.Message, "DBconn - ConnectToDB()");

                }

                return false;

            }

            return true;

        }



        /// <summary>

        /// Database Open 여부 확인

        /// </summary>

        public static bool IsDBConnected

        {

            get

            {

                if (conn.State != System.Data.ConnectionState.Open)

                {

                    return false;

                }

                return true;

            }

        }



        /// <summary>

        /// Database 해제

        /// </summary>

        public static void Close()

        {

            if (IsDBConnected)

                DBConn.Close();

        }
    }
}
