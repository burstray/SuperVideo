﻿using SuperVideo.tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using static System.Net.Mime.MediaTypeNames;
using System.Web.UI.WebControls;

namespace SuperVideo.common
{
    public class HttpServer
    {
        public const int BufferSize = 4096;
        private bool IsStopped = false;

        public int Port { get; set; }
        public string Key { get; set; }
        public string Token { get; set; }
        public bool IsRunning { get; private set; }

        HttpListener httpListener = new HttpListener();

        public HttpServer()
        {
            Port = 18888;
        }

        public void Start()
        {
            if (IsRunning) return;

            httpListener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            httpListener.Prefixes.Add(string.Format("http://localhost:{0}/", Port));
            httpListener.Start();
            new Thread(() =>
            {
                IsRunning = true;

                while (true)
                {
                    try
                    {
                        if (IsStopped) break;
                        HttpListenerContext ctx = httpListener.GetContext();
                        string ua = ctx.Request.UserAgent;
                        //string token = ctx.Request.QueryString["token"];
                        //if (string.IsNullOrEmpty(ua) || !ua.StartsWith("VLC") || !ua.StartsWith("Mozilla"))//if (string.IsNullOrEmpty(ua) || !ua.StartsWith("VLC") || !ua.StartsWith("Mozilla"))
                        //{
                        //    ctx.Response.StatusCode = 403;
                        //    ctx.Response.Close();
                        //    continue;
                        //}

                        string querys = ctx.Request.RawUrl;//Path.GetFileName(ctx.Request.RawUrl);


                        //string category = HttpUtility.ParseQueryString(querys).Get("category"); //避免中文乱码
                        //string file = HttpUtility.ParseQueryString(querys).Get("file");
                        //string filename = frmMain.VideoDir + category + "\\" + file;


                        string filename = frmMain.VideoDir + "\\" + querys.Replace('/', '\\');


                        //if (string.IsNullOrEmpty(token) || !token.Equals(Token) || !File.Exists(filename))
                        //{
                        //    ctx.Response.StatusCode = 404;
                        //    ctx.Response.Close();
                        //    continue;
                        //}

                        OutputFile(ctx, filename);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Start error={0}", ex.Message);
                    }
                }
                if (httpListener.IsListening) httpListener.Stop();
                IsRunning = false;
            }).Start();

        }

        private void OutputFile(HttpListenerContext ctx, string filename)
        {
            ThreadPool.QueueUserWorkItem((obj) =>
            {
                try
                {
                    Console.WriteLine("requesting " + filename);
                    ctx.Response.StatusCode = 200;
                    ctx.Response.KeepAlive = true;

                    if (filename.ToLower().EndsWith(".mp4"))
                    {
                        ctx.Response.AddHeader("Content-Type", GetContentType(filename));
                        //ctx.Response.AddHeader("content-disposition", "attachment;filename=" + HttpUtility.UrlEncode(Path.GetFileName(filename)));

                        Stream writer = ctx.Response.OutputStream;
                        FileStream fsr = new FileStream(filename, FileMode.Open, FileAccess.Read);

                        byte[] readBytes = new byte[BufferSize + 16];
                        int len = 0;
                        while ((len = fsr.Read(readBytes, 0, readBytes.Length)) > 0)
                        {
                            if (IsStopped) break;
                            try
                            {
                                if (len < (BufferSize + 16))
                                {
                                    byte[] rest = new byte[len];
                                    Array.Copy(readBytes, rest, len);

                                    try
                                    {
                                        byte[] en = SecurityUtils.AesDecrypt(Key, rest);
                                        if (en != null)
                                        {
                                            writer.Write(en, 0, en.Length);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine("OutputFile exception: " + ex.Message);//Ray 20220825 added
                                    }
                                }
                                else
                                {
                                    byte[] en = SecurityUtils.AesDecrypt("1234560000000000", readBytes);
                                    writer.Write(en, 0, en.Length);
                                }
                                writer.Flush();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("OutputFile error={0}", ex.Message);
                                break;
                            }
                        }

                        fsr.Close();
                        writer.Close();
                        ctx.Response.Close();
                    }
                    else
                    {
                        ctx.Response.AddHeader("Content-Type", GetContentType(filename));

                        Stream wri = ctx.Response.OutputStream;
                        FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);

                        byte[] reader = new byte[fs.Length];
                        fs.Read(reader, 0, reader.Length);
                        wri.Write(reader, 0, reader.Length);
                        wri.Flush();
                        fs.Close();
                        wri.Close();
                        ctx.Response.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("output file exception " + ex.Message);
                }
            });
        }

        public void Stop()
        {
            IsStopped = true;
            if (httpListener.IsListening) httpListener.Stop();
        }

        private string GetContentType(string filename)
        {
            if (filename.ToLower().EndsWith(".mp4"))
            {
                return "video/mpeg4";
            }
            else if (filename.ToLower().EndsWith(".xml"))
            {
                return "text/xml; charset=UTF-8";
            }
            else
            {
                return "text/html; charset=UTF-8";
            }

        }
    }
}
