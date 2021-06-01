using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Master
{
    public partial class Form1 : Form
    {
        private List<string> images = new List<string>();
        private List<int> imageStatus = new List<int>();
        private List<int> ports = new List<int>() { 2020, 2030, 2040, 2050 };
        private List<bool> portStatus = new List<bool>() { false, false, false, false };
        private TcpListener listener = new TcpListener(IPAddress.Any, 9999);

        public Form1()
        {
            InitializeComponent();

            dataGridView1.Columns[0].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.Columns[1].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.Columns[2].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        private void SelectFolder_Click(object sender, EventArgs e)
        {
            label3.Text = "Programa sustabdyta.";
            progressBar1.Value = 0;

            images.Clear();
            imageStatus.Clear();
            for (int i = 0; i < portStatus.Count; i++)
            {
                portStatus[i] = false;
            }

            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string selectedPath = folderBrowserDialog1.SelectedPath;
                    string[] temp = Directory.GetFiles(selectedPath, "*.jpg");
                    foreach (var str in temp)
                    {
                        images.Add(str);
                        imageStatus.Add(0);
                    }
                    textBox1.Text = selectedPath;

                    progressBar1.Maximum = images.Count;

                    var t0 = new Thread(() =>
                    {
                        UpdateDatagrid();
                    });
                    t0.Start();
                }
                catch (Exception exc)
                {
                    MessageBox.Show("Exception while selecting category, details: " + exc);
                }
            }
        }

        private void UpdateDatagrid()
        {
            while (true)
            {
                Thread.Sleep(1000);

                try
                {
                    dataGridView1.Invoke((MethodInvoker)delegate
                    {
                        int saveRow = 0;
                        if (dataGridView1.Rows.Count > 0 && dataGridView1.FirstDisplayedCell != null)
                            saveRow = dataGridView1.FirstDisplayedCell.RowIndex;

                        dataGridView1.Rows.Clear();

                        for (int i = 0; i < images.Count; i++)
                        {
                            i = dataGridView1.Rows.Add();
                            dataGridView1.Rows[i].Cells["Column1"].Value = i;
                            dataGridView1.Rows[i].Cells["Column2"].Value = images[i];
                            dataGridView1.Rows[i].Cells["Column3"].Value = imageStatus[i];
                        }

                        if (saveRow != 0 && saveRow < dataGridView1.Rows.Count)
                            dataGridView1.FirstDisplayedScrollingRowIndex = saveRow;
                    });
                }
                catch (Exception exc)
                {
                    MessageBox.Show("Exception while updating the datagrid, details: " + exc);
                }
            }
        }

        private void TrySendingFiles_Click(object sender, EventArgs e)
        {
            label3.Text = "Programa veikia...";

            var t1 = new Thread(() =>
            {
                ListenForResults();
            });
            t1.Start();

            var t2 = new Thread(() =>
            {
                ProgressUpdate();
            });
            t2.Start();

            var t3 = new Thread(() =>
            {
                ConnectionLostCheck();
            });
            t3.Start();

            var t4 = new Thread(() =>
            {
                IsFileStillAvailable();
            });
            t4.Start();

            if (images.Count == 0)
            {
                MessageBox.Show("Paveikslėlių sąrašas yra tuščias!");
            }
            else
            {
                var t5 = new Thread(() =>
                {
                    while (true)
                    {
                        for (int i = 0; i < ports.Count; i++)
                        {
                            if (!portStatus[i])
                            {
                                int limitFiveFiles = 0;

                                for (int j = 0; j < images.Count; j++)
                                {
                                    if (limitFiveFiles == 5)
                                        break;

                                    if (imageStatus[j] == 0)
                                    {
                                        limitFiveFiles++;

                                        try
                                        {
                                            IPAddress ip = IPAddress.Parse("127.0.0.1");
                                            IPEndPoint end = new IPEndPoint(ip, ports[i]);
                                            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

                                            byte[] fileNameBytes = Encoding.ASCII.GetBytes(Path.GetFileName(images[j]));
                                            byte[] fileNameLen = BitConverter.GetBytes(Path.GetFileName(images[j]).Length);
                                            byte[] preBuff = new byte[4 + fileNameBytes.Length];

                                            fileNameLen.CopyTo(preBuff, 0);
                                            fileNameBytes.CopyTo(preBuff, 4);

                                            sock.Connect(end);
                                            portStatus[i] = true;
                                            if (File.Exists(images[j]))
                                                sock.SendFile(images[j], preBuff, null, TransmitFileOptions.UseDefaultWorkerThread);
                                            sock.Close();
                                        }
                                        catch (SocketException exc)
                                        {
                                            portStatus[i] = false;
                                            break;
                                        }

                                        imageStatus[j] = ports[i];
                                    }
                                }
                            }
                        }
                    }
                });
                t5.Start();
            }
        }

        public void IsFileStillAvailable()
        {
            while (true)
            {
                Thread.Sleep(1000);

                string selectedPath = folderBrowserDialog1.SelectedPath;
                List<string> temp = new List<String>(Directory.GetFiles(selectedPath, "*.jpg"));

                for (int i = 0; i < images.Count; i++)
                    if (!temp.Contains(images[i]))
                    {
                        images.RemoveAt(i);
                        imageStatus.RemoveAt(i);
                        break;
                    }
            }
        }

        public void ListenForResults()
        {
            listener.Start();

            while (true)
            {
                using (var client = listener.AcceptTcpClient())
                using (var stream = client.GetStream())
                {
                    byte[] fileNameLengthBytes = new byte[4];
                    stream.Read(fileNameLengthBytes, 0, 4);
                    int fileNameLength = BitConverter.ToInt32(fileNameLengthBytes, 0);
                    byte[] fileNameBytes = new byte[fileNameLength];
                    stream.Read(fileNameBytes, 0, fileNameLength);
                    string fileName = Encoding.ASCII.GetString(fileNameBytes, 0, fileNameLength);

                    using (var output = File.Create(fileName))
                    {
                        var buffer = new byte[1024];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, bytesRead);
                        }
                    }

                    for (int i = 0; i < images.Count; i++)
                        if (Path.GetFileNameWithoutExtension(images[i]) == Path.GetFileNameWithoutExtension(fileName))
                            imageStatus[i] = 1;

                    for (int i = 0; i < ports.Count; i++)
                        if (!imageStatus.Contains(ports[i]) && portStatus[i] == true)
                            portStatus[i] = false;
                }
            }
        }

        public void ProgressUpdate()
        {
            while (true)
            {
                int count = 0;
                for (int i = 0; i < images.Count; i++)
                    if (imageStatus[i] == 1)
                        count++;

                if (count == images.Count)
                {
                    label3.Invoke((MethodInvoker)delegate
                    {
                        label3.Text = "Darbas baigtas!";
                    });
                    CombineResults();
                }

                progressBar1.Invoke((MethodInvoker)delegate
                {
                    progressBar1.Value = count;
                });
            }
        }

        public void ConnectionLostCheck()
        {
            while (true)
            {
                for (int i = 0; i < ports.Count; i++)
                {
                    if (portStatus[i] == true)
                    {
                        try
                        {
                            List<int> placement = new List<int>();
                            int count = 0;

                            for (int j = 0; j < images.Count; j++)
                                if (imageStatus[j] == ports[i])
                                    placement.Add(j);

                            Thread.Sleep(10000);

                            count = 0;
                            for (int j = 0; j < placement.Count; j++)
                            {
                                int temp = placement[j];
                                if (imageStatus[placement[j]] == ports[i])
                                    count++;
                            }

                            if (count == placement.Count && count != 0)
                            {
                                for (int j = 0; j < placement.Count; j++)
                                    imageStatus[placement[j]] = 0;
                                portStatus[i] = false;
                            }
                        }
                        catch (Exception exc)
                        {
                            MessageBox.Show("Exception while checking if connection is lost, details: " + exc);
                        }
                    }
                }
            }
        }

        public void CombineResults()
        {
            Thread.Sleep(500);

            try
            {
                string mainDir = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

                if (File.Exists(mainDir + $@"\result.txt"))
                    File.Delete(mainDir + $@"\result.txt");

                List<string> filesIncluded = new List<string>(Directory.GetFiles(mainDir, "*.txt"));

                using (var outputStream = File.Create(mainDir + $@"\result.txt"))
                    foreach (var inputFilePath in filesIncluded)
                    {
                        using (var inputStream = File.OpenRead(inputFilePath))
                            inputStream.CopyTo(outputStream);
                        //File.Delete(inputFilePath);
                    }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception while combining the results, details: " + exc);
            }
        }
    }
}
