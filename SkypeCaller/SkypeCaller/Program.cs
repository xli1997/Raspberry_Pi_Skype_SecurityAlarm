using System;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace SkypeCaller
{
    class Program
    {
        enum Status
        {
            Start,
            Detect,
            Trigger,
            DisArm,
            Error
        };

        static bool PingRaspberryPi()
        {
            var ping = new System.Net.NetworkInformation.Ping();
            var result = ping.Send("192.168.1.8");  
            if (result.Status == System.Net.NetworkInformation.IPStatus.Success)
                return true;
            else
                return false;
        }

        static void Main(string[] args)
        {
            string html = string.Empty;
            string url = @"http://192.168.1.3:8090";

            string old_name = ".\\log.txt";
            if (File.Exists(old_name))
            {
                Console.WriteLine("start up, rename log.txt");
                File.Move(old_name, ".\\" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt");
            }

            // Use TimeSpan constructor to specify:
            // ... Days, hours, minutes, seconds, milliseconds.
            TimeSpan time_span = new TimeSpan(0, 0, 30, 0, 0);
            DateTime start_time_stamp = DateTime.Now;
            DateTime trigger_time_stamp = start_time_stamp - time_span - time_span;

            Status status = Status.Start;
            bool raspberryStatus = true;
            bool prev_raspberryStatus = true;

            while (true)
            {
                StreamWriter writer = new StreamWriter(old_name);
                int count = 0;

                while (count < 120960)
                {
                    count++;
                    try
                    {
                        string time_text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        // reset the webpage if raspberry is off
                        prev_raspberryStatus = raspberryStatus;
                        if (PingRaspberryPi())
                        {
                            raspberryStatus = true;
                        }
                        else
                        {
                            raspberryStatus = false;
                        }
                        if (prev_raspberryStatus && !raspberryStatus)
                        {
                            Console.WriteLine(time_text + " RaspberryPi turned off");
                            writer.WriteLine(time_text + " RaspberryPi turned off");
                        }
                        else if (!prev_raspberryStatus && raspberryStatus)
                        {
                            Console.WriteLine(time_text + " RaspberryPi turned on");
                            writer.WriteLine(time_text + " RaspberryPi turned on");
                        }


                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                        request.AutomaticDecompression = DecompressionMethods.GZip;

                        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                        using (Stream stream = response.GetResponseStream())
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            html = reader.ReadToEnd();
                        }

                        string pattern_Trigger = @"id='camerasYes' value='always' checked='checked'";
                        string pattern_Detect = @"id='camerasNo' value='never' checked='checked'";
                        string pattern_DisArm = @"id='camerasOver' value='mouseover' checked='checked'";


                        Match match_Detect = Regex.Match(html, pattern_Detect);
                        Match match_Trigger = Regex.Match(html, pattern_Trigger);
                        Match match_DisArm = Regex.Match(html, pattern_DisArm);

                        if (match_Trigger.Success)
                        {
                            if (status != Status.Trigger)
                            {
                                Console.WriteLine(time_text + "    Arm TRIGGERED!");
                                writer.WriteLine(time_text + "    Arm TRIGGERED!");
                            }

                            status = Status.Trigger;

                            DateTime current_time_stamp = DateTime.Now;
                            if (current_time_stamp - trigger_time_stamp > time_span)
                            {
                                Console.WriteLine(time_text + "    Arm will call cellphone");
                                writer.WriteLine(time_text + "    Arm will call cellphone");
                                trigger_time_stamp = current_time_stamp;
                                string strCmdText;
                                strCmdText = "";
                                System.Diagnostics.Process.Start(@"..\..\Skype4Py-master\caller\sms.py", strCmdText);
                            }

                        }
                        else if (match_Detect.Success)
                        {
                            if (status != Status.Detect)
                            {
                                Console.WriteLine(time_text + "    Arm detecting...");
                                writer.WriteLine(time_text + "    Arm detecting...");
                            }
                            status = Status.Detect;
                        }
                        else if (match_DisArm.Success)
                        {
                            if (status != Status.DisArm)
                            {
                                Console.WriteLine(time_text + " DisArm");
                                writer.WriteLine(time_text + " DisArm");
                            }
                            status = Status.DisArm;
                        }
                        else
                        {
                            if (status != Status.Error)
                            {
                                Console.WriteLine(time_text + " something is wrong");
                                writer.WriteLine(time_text + " something is wrong");
                            }
                            status = Status.Error;
                        }
                        //Pause for 5 seconds
                        System.Threading.Thread.Sleep(5000);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        writer.WriteLine(e.Message);
                    }
                    finally
                    {
                        writer.Flush();
                    }
                }

                string new_name = ".\\" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt";
                writer.Close();
                writer.Dispose();
                File.Move(old_name, new_name);
            }
        }
    }
}
