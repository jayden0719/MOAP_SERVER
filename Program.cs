using System;
using System.Data;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using MOAP;

namespace MOAP_SERVER
{
    class MainApp
    {

        static void Main(string[] args)
        {
            Thread EventHandler = new Thread(ServerTcp);
            EventHandler.Start();
           
        }

        private static void ServerTcp()
        {
            TcpListener server = null;
            uint msgId = 0;
            const int bindPort = 5425; //서버 포트
            try
            {
                IPEndPoint localAddress = new IPEndPoint(0, bindPort);
                server = new TcpListener(localAddress);
                server.Start();
                Console.WriteLine("서버 IP : " + GetLocalIP() + " \r\n서버 포트 : " + bindPort);
                Console.WriteLine("서버 구동 시작...");

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("클라이언트 접속 : {0}", ((IPEndPoint)client.Client.RemoteEndPoint).ToString());

                    NetworkStream stream = client.GetStream();
                    Message reqMsg = MessageUtil.Receive(stream); //요청 RECEIVE

                    if (reqMsg.Header.MOATYPE != CONSTANTS.REQ_FAX_SEND && reqMsg.Header.MOATYPE != CONSTANTS.REQ_FILE_SEND && reqMsg.Header.MOATYPE != CONSTANTS.REQ_MSG_SEND)
                    {
                        stream.Close();
                        client.Close();
                        continue;
                    }

                    // 파일 전송 요청
                    if (reqMsg.Header.MOATYPE == CONSTANTS.REQ_FILE_SEND)
                    {
                        string folderPath = System.IO.Directory.GetCurrentDirectory() + @"\UploadFolder";
                        DirectoryInfo di = new DirectoryInfo(folderPath);
                        if(di.Exists == false)
                        {
                            di.Create();
                        }

                        BodyRequest reqBody = (BodyRequest)reqMsg.Body;
                        Console.WriteLine("파일 업로드 요청이 왔습니다. 수락하나요?? (yes/no)");
                        string answer = Console.ReadLine();

                        Message rspMsg = new Message();
                        rspMsg.Body = new BodyResponse()
                        {
                            MOAID = reqMsg.Header.MOAID,
                            RESPONSE = CONSTANTS.ACCTEPTED
                        };
                        rspMsg.Header = new Header()
                        {
                            MOAID = msgId++,
                            MOATYPE = CONSTANTS.REP_FILE_SEND,
                            BODYLEN = (uint)rspMsg.Body.GetSize(),
                            FRAGMENTED = CONSTANTS.NOT_FRAGMENTED,
                            LASTMSG = CONSTANTS.LASTMSG,
                            SEQ = 0
                        };

                        if (answer != "yes")
                        {
                            rspMsg.Body = new BodyResponse()
                            {
                                MOAID = reqMsg.Header.MOAID,
                                RESPONSE = CONSTANTS.DENIED
                            };
                            MessageUtil.Send(stream, rspMsg);
                            stream.Close();
                            client.Close();
                            continue;
                        }
                        else
                        {
                            MessageUtil.Send(stream, rspMsg);
                        }
                        Console.WriteLine("파일 전송을 시작합니다.");

                    
                            long fileSiZe = reqBody.FILESIZE; //전송 받은 파일크기
                            string fileName = Encoding.Default.GetString(reqBody.FILENAME); //전송받은 파일명
                            string filename = System.IO.Path.GetFileName(fileName);
                            FileStream file = new FileStream(folderPath + "\\" + filename, FileMode.Create, FileAccess.ReadWrite);

                            uint? dataMsgId = null;
                            ushort prevSeq = 0;
                            while ((reqMsg = MessageUtil.Receive(stream)) != null) //요청 RECEIVE
                            {
                                Console.Write("#");
                                if (reqMsg.Header.MOATYPE != CONSTANTS.FILE_SEND_DATA)
                                {
                                    break;
                                }
                                if (dataMsgId == null)
                                {
                                    dataMsgId = reqMsg.Header.MOAID;
                                }
                                else
                                {
                                    if (dataMsgId != reqMsg.Header.MOAID)
                                    {
                                        break;
                                    }
                                }
                                if (prevSeq++ != reqMsg.Header.SEQ)
                                {
                                    Console.WriteLine("{0}, {1}", prevSeq, reqMsg.Header.SEQ);
                                    break;
                                }
                                file.Write(reqMsg.Body.GetBytes(), 0, reqMsg.Body.GetSize());

                                if (reqMsg.Header.FRAGMENTED == CONSTANTS.NOT_FRAGMENTED)
                                {
                                    break;
                                }
                                if (reqMsg.Header.LASTMSG == CONSTANTS.LASTMSG)
                                {
                                    break;
                                }
                            }
                            long recvFileSize = file.Length;
                            file.Close();

                            Console.WriteLine();
                            Console.WriteLine("수신 파일 크기 : {0} bytes", recvFileSize);

                            //파일 전송 완료 이후의 메세지
                            Message rstMsg = new Message();
                            rstMsg.Body = new BodyResult()
                            {
                                MOAID = reqMsg.Header.MOAID,
                                RESULT = CONSTANTS.SUCCESS
                            };
                            rstMsg.Header = new Header()
                            {
                                MOAID = msgId++,
                                MOATYPE = CONSTANTS.FILE_SEND_RES,
                                BODYLEN = (uint)rstMsg.Body.GetSize(),
                                FRAGMENTED = CONSTANTS.NOT_FRAGMENTED,
                                LASTMSG = CONSTANTS.LASTMSG,
                                SEQ = 0
                            };

                            if (fileSiZe == recvFileSize)
                            {
                                MessageUtil.Send(stream, rstMsg);
                            }
                            else
                            {
                                rstMsg.Body = new BodyResult()
                                {
                                    MOAID = reqMsg.Header.MOAID,
                                    RESULT = CONSTANTS.FAIL
                                };
                                MessageUtil.Send(stream, rstMsg);
                            }
                            Console.WriteLine("파일 전송을 마쳤습니다");
                            Console.WriteLine("====================");
                            Console.WriteLine("서버를 종료하시겠습니까?(네/아니오)");
                            string retry = Console.ReadLine();
                            if (retry.Equals("네"))
                            {
                                stream.Close();
                                client.Close();
                            Console.WriteLine("서버가 종료되었습니다");
                                return;
                            }
                            else
                            {
                            }
                    }
                    // 팩스 전송결과 기능
                    else if (reqMsg.Header.MOATYPE == CONSTANTS.REQ_FAX_SEND)
                    {
                        FAXBodyRequest reqBody = (FAXBodyRequest)reqMsg.Body;
                        Console.WriteLine("팩스 전송결과 요청이 왔습니다. 수락하나요?? (yes/no)");
                        string answer = Console.ReadLine();

                        string sUserId = Encoding.UTF8.GetString(reqBody.USERID);
                        string service = Encoding.UTF8.GetString(reqBody.SERVICE);
                        string dtRecv = Encoding.UTF8.GetString(reqBody.REQDATE).ToString().Trim();
                        string dtEnd = Encoding.UTF8.GetString(reqBody.ENDDATE).ToString().Trim();

                        // 바이트 스트링 변환 데이터 파싱 재구성(MSSQL 문자열 날짜 및 시간 변환 에러)
                        string dRecv = dtRecv.Substring(0, 4) + "-" + dtRecv.Substring(5, 2) + "-" + dtRecv.Substring(8, 2);
                        string dEnd = dtEnd.Substring(0, 4) + "-" + dtEnd.Substring(5, 2) + "-" + dtEnd.Substring(8, 2);

                        try
                        {
                            lock (DBconn.DBConn)
                            {
                                if (!DBconn.IsDBConnected)
                                {
                                    Console.WriteLine("DB연결을 확인하세요");
                                    return;
                                }
                                else
                                {
                                    if (answer != "yes")
                                    {
                                        Message rspMsg = new Message();
                                        rspMsg.Body = new BodyResponse()
                                        {
                                            MOAID = reqMsg.Header.MOAID,
                                            RESPONSE = CONSTANTS.DENIED
                                        };
                                        rspMsg.Header = new Header()
                                        {
                                            MOAID = msgId++,
                                            MOATYPE = CONSTANTS.REP_FILE_SEND, // 파일 응답 메세지로 재활용
                                            BODYLEN = (uint)rspMsg.Body.GetSize(),
                                            FRAGMENTED = CONSTANTS.NOT_FRAGMENTED,
                                            LASTMSG = CONSTANTS.LASTMSG,
                                            SEQ = 0
                                        };
                                        MessageUtil.Send(stream, rspMsg);
                                        stream.Close();
                                        client.Close();
                                        continue;
                                    }
                                    else
                                    {
                                        SqlDataAdapter adapter;
                                        string sql;
                                        DataSet ds = new DataSet();

                                        //equals, == 는 두 문자열의 객체!가 동일한지, compareTo는 문자열 정렬값이 동일한지 비교
                                        if (service.CompareTo("MOA") == 0) //모아샷
                                        {
                                            sql = string.Format("select sJobID, sFromInfo, sUserID, ntotalcnt, nsuccesscnt, (ntotalcnt - nsuccesscnt) as nfailcnt " +
                                                                "from [75].FAX.dbo.joblog with(NOLOCK)\r\n  where sUserID='{0}' and dtrecvtime>='{1}' and dtEndTime <'{2}'", sUserId, dRecv, dEnd);
                                            adapter = new SqlDataAdapter(sql, DBconn.DBConn);
                                        }
                                        else //연동
                                        {
                                            sql = string.Format("select sJobID, sFromInfo, sUserID, ntotalcnt, nsuccesscnt, (ntotalcnt - nsuccesscnt) as nfailcnt " +
                                                                "from [75].ASP.dbo.joblog with(NOLOCK)\r\n  where nsvctype in (1,2) sUserID='{0}' and dtrecvtime>='{1}' and dtEndTime <'{2}'", sUserId, dRecv, dEnd);
                                            adapter = new SqlDataAdapter(sql, DBconn.DBConn);

                                        }
                                        adapter.Fill(ds);

                                        //조회한 데이터 전송
                                        Message DataMsg = new Message();
                                        byte[] bytes = CompressDataSet(ds);
                                        DataMsg.Body = new FAXBodyData(bytes);
                                        DataMsg.Header = new Header()
                                        {
                                            MOAID = msgId++,
                                            MOATYPE = CONSTANTS.FAX_SEND_DATA,
                                            BODYLEN = (uint)DataMsg.Body.GetSize(),
                                            FRAGMENTED = CONSTANTS.NOT_FRAGMENTED,
                                            LASTMSG = CONSTANTS.LASTMSG,
                                            SEQ = 0
                                        };
                                        if (ds is null)
                                        {
                                            Console.WriteLine("데이터 조회 건이 없습니다");
                                        }

                                        MessageUtil.Send(stream, DataMsg);
                                        Console.WriteLine("팩스 결과 전송을 마쳤습니다");
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                        //팩스 전송 이후 완료 메세지
                        Message rstMsg;
                        while ((rstMsg = MessageUtil.Receive(stream)) != null)
                        {
                            FAXBodyResult RES = (FAXBodyResult)rstMsg.Body;
                            if (RES.RESULT == CONSTANTS.FAIL)
                            {
                                Console.WriteLine("팩스 전송결과 전송 실패");
                            }
                            else
                            {
                                Console.WriteLine("팩스 전송결과 전송 성공!!!");
                                Console.WriteLine("=========================");
                                Console.WriteLine("서버를 종료하시겠습니까?(네/아니오)");
                                string retry = Console.ReadLine();
                                if (retry != "네")
                                {
                                    break;
                                }
                                else
                                {
                                    stream.Close();
                                    client.Close();
                                    Console.WriteLine("서버가 종료되었습니다");
                                    return;
                                }
                            }
                        }

                    // 문자 전송결과 조회
                    }
                    else if (reqMsg.Header.MOATYPE == CONSTANTS.REQ_MSG_SEND)
                    {
                        MsgBodyRequest reqBody = (MsgBodyRequest) reqMsg.Body;
                        Console.WriteLine("문자 전송결과 요청이 왔습니다. 수락하나요?? (yes/no)");
                        string answer = Console.ReadLine();
                      
                        string sUserId = Encoding.UTF8.GetString(reqBody.USERID);
                        string service = Encoding.UTF8.GetString(reqBody.SERVICE);                       
                        string dtRecv = Encoding.UTF8.GetString(reqBody.REQDATE).ToString().Trim();                       
                        string dtEnd = Encoding.UTF8.GetString(reqBody.ENDDATE).ToString().Trim();

                        string dRecv = dtRecv.Substring(0, 4) + "-" + dtRecv.Substring(5, 2) + "-" + dtRecv.Substring(8, 2);
                        string dEnd = dtEnd.Substring(0, 4) + "-" + dtEnd.Substring(5, 2) + "-" + dtEnd.Substring(8, 2);                  

                        try
                        {
                            lock (DBconn.DBConn)
                            {
                                if (!DBconn.IsDBConnected)
                                {
                                    Console.WriteLine("DB연결을 확인하세요");
                                    return;
                                }
                                else
                                {
                                    if(answer != "yes")
                                    {
                                        Message rspMsg = new Message();
                                        rspMsg.Body = new BodyResponse()
                                        {
                                            MOAID = reqMsg.Header.MOAID,
                                            RESPONSE = CONSTANTS.DENIED
                                        };
                                        rspMsg.Header = new Header()
                                        {
                                            MOAID = msgId++,
                                            MOATYPE = CONSTANTS.REP_FILE_SEND,
                                            BODYLEN = (uint)rspMsg.Body.GetSize(),
                                            FRAGMENTED = CONSTANTS.NOT_FRAGMENTED,
                                            LASTMSG = CONSTANTS.LASTMSG,
                                            SEQ = 0
                                        };                                        
                                            MessageUtil.Send(stream, rspMsg);
                                            stream.Close();
                                            client.Close();
                                            continue;
                                    }
                                    else
                                    {
                                        SqlDataAdapter adapter;
                                        string sql;
                                        DataSet ds = new DataSet();

                                        //equals, == 는 두 문자열의 객체!가 동일한지, compareTo는 문자열 정렬값이 동일한지 비교
                                        if (service.CompareTo("MOA")==0) //모아샷
                                        {
                                            sql = string.Format("select sJobID, sFromInfo, sUserID, ntotalcnt, nsuccesscnt, (ntotalcnt - nsuccesscnt) as nfailcnt " +
                                                "from joblog with(NOLOCK)\r\n  where sUserID='{0}' and dtrecvtime >= Convert(Datetime, '{1}') and dtEndTime < Convert(Datetime, '{2}')", sUserId, dRecv, dEnd);
                                            adapter = new SqlDataAdapter(sql, DBconn.DBConn);
                                        }
                                        else //연동
                                        {
                                            sql = string.Format("select sJobID, sFromInfo, sUserID, ntotalcnt, nsuccesscnt, (ntotalcnt - nsuccesscnt) as nfailcnt " +
                                                "from [75].ASP.dbo.joblog with(NOLOCK)\r\n  where nsvctype in (3,5,6) and sUserID='{0}' and dtrecvtime >= '{1}' and dtEndTime <'{2}'", sUserId, dRecv, dEnd);
                                            adapter = new SqlDataAdapter(sql, DBconn.DBConn);
                                        }
                                        adapter.Fill(ds);

                                        //조회한 데이터 전송
                                        Message DataMsg = new Message();
                                        byte[] bytes = CompressDataSet(ds);
                                        DataMsg.Body = new MSGBodyData(bytes);
                                        DataMsg.Header = new Header()
                                        {
                                            MOAID = msgId++,
                                            MOATYPE = CONSTANTS.MSG_SEND_DATA,
                                            BODYLEN = (uint)DataMsg.Body.GetSize(),
                                            FRAGMENTED = CONSTANTS.NOT_FRAGMENTED,
                                            LASTMSG = CONSTANTS.LASTMSG,
                                            SEQ = 0
                                        };
                                        if (ds is null)
                                        {
                                            Console.WriteLine("데이터 조회 건이 없습니다");
                                        }
                                    
                                        MessageUtil.Send(stream, DataMsg);
                                        Console.WriteLine("문자 결과 전송을 마쳤습니다");
                                    }                                    
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                        //문자 전송 이후 완료 메세지
                        Message rstMsg; 
                        while((rstMsg= MessageUtil.Receive(stream)) != null)
                        {
                            MSGBodyResult result = (MSGBodyResult)rstMsg.Body;
                            if (result.RESULT == CONSTANTS.FAIL)
                            {
                                Console.WriteLine("문자 전송결과 전송 실패");
                            }
                            else
                            {
                                Console.WriteLine("문자 전송결과 전송 성공!!!");
                                Console.WriteLine("=========================");
                                Console.WriteLine("서버를 종료하시겠습니까?(네/아니오)");
                                string retry = Console.ReadLine();
                                if (retry != "네")
                                {
                                    break;
                                }
                                else
                                {
                                    stream.Close();
                                    client.Close();
                                    Console.WriteLine("서버가 종료되었습니다");
                                    return;
                                }
                            }                          
                        }                        
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                server.Stop();
            }
            Console.WriteLine("서버를 종료합니다.");
        }

        private static string GetLocalIP()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            string LocalIP = string.Empty;

            for (int i = 0; i < host.AddressList.Length; i++)
            {
                if (host.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    LocalIP = host.AddressList[i].ToString();
                    break;
                }
            }
            return LocalIP;
        }

        public static byte[] CompressDataSet(DataSet ds)
        {
            Byte[] data;
            MemoryStream mem = new MemoryStream();
            GZipStream zip = new GZipStream(mem, CompressionMode.Compress);
            ds.WriteXml(zip, XmlWriteMode.WriteSchema);
            zip.Close();
            data = mem.ToArray();
            mem.Close();
            return data;   
        }
    }
}

