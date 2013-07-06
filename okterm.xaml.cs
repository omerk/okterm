using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.Windows.Threading;
using System.Collections.Concurrent;

namespace okterm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        #region Globals
        SerialPort serialport = new SerialPort();
        List<string> baudrates = new List<string>(){"9600", "19200", "57600", "115200"};
        List<string> portList = new List<string>();
        ConcurrentQueue<char> receiveQueue = new ConcurrentQueue<char>();
        DispatcherTimer receiveTimer = new System.Windows.Threading.DispatcherTimer();
        Boolean portOpen = false;
        #endregion

        #region Serial Port stuff

        void sendData(String str)
        {
            if (portOpen)
            {
                serialport.Write(str);
            }

        }

        //FIXME: get rid of the ConcurrentQueue?
        void receiveData(object sender, SerialDataReceivedEventArgs ev)
        {
            int buflen = serialport.BytesToRead;
            char[] recBuf = new char[buflen];

            try
            {
                serialport.Read(recBuf, 0, buflen);
                for (int index = 0; index < buflen; index++)
                {
                    receiveQueue.Enqueue(recBuf[index]);
                }
            }
            catch (Exception e)
            {
                addTextError(e.ToString());
            }
        }

        private void printRecvBuf()
        {
            char c;

            try
            {
                while (receiveQueue.TryDequeue(out c))
                {
                    addTextReceived(c.ToString());
                }

            }
            catch (Exception e)
            {
                addTextError(e.ToString());
            }
        }

        void listPorts()
        {
            addTextInfo("listing ports");

            portList.Clear();
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                portList.Add(port);
            }

        }

        #endregion

        #region Terminal text functions
        void addText(String text, Brush color)
        {
            TextRange tr = new TextRange(txtTerminal.Document.ContentEnd, txtTerminal.Document.ContentEnd);
            tr.Text = text;
            tr.ApplyPropertyValue(TextElement.ForegroundProperty, color);
            //tr.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);

            if (chkBoxScroll.IsChecked == true)
            {
                txtTerminal.ScrollToEnd();
            }
        }

        void addTextSent(String text)
        {
            addText(text, Brushes.Orange);
        }

        void addTextReceived(String text)
        {
            addText(text, Brushes.LimeGreen);
        }

        void addTextError(String text)
        {
            addText(text, Brushes.Red);
        }

        void addTextInfo(String text)
        {
            addText(text, Brushes.LightSkyBlue);
        }
        #endregion

        #region Text prompt functions
        private void promptSend(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if (portOpen)
                {
                    String s = txtPrompt.Text;

                    if (chkBoxLF.IsChecked == true)
                    {
                        s += "\n";
                    }

                    if (chkBoxCR.IsChecked == true)
                    {
                        s += "\r";
                    }
                    sendData(s);
                    addTextSent(s);
                    txtPrompt.Clear();
                }
                else
                {
                    addTextError("Not connected\n");
                }
            }

        }
        #endregion

        #region Buttons and control stuff
        private void btnControl_Click(object sender, RoutedEventArgs e)
        {
            if (portOpen)
            {
                try
                {
                    serialport.Close();
                    receiveTimer.Stop();

                    portOpen = false;
                    btnControl.Content = "Open Port";
                    addTextInfo("Port closed\n");
                }
                catch (Exception ex)
                {
                    addTextError(ex.ToString());
                }

            }
            else
            {
                try
                {
                    serialport.PortName = comboBoxPorts.SelectedItem.ToString();
                    serialport.BaudRate = int.Parse(comboBoxSpeeds.SelectedItem.ToString());
                    serialport.Open();
                    serialport.DataReceived += new SerialDataReceivedEventHandler(receiveData);
                    
                    receiveTimer.Start();

                    portOpen = true;
                    btnControl.Content= "Close Port";
                    addTextInfo("Port opened\n");

                }
                catch (Exception ex)
                {
                    addTextError(ex.ToString());
                }
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            txtTerminal.SelectAll();
            txtTerminal.Selection.Text = "";
        }

        private void refreshPorts(object sender, MouseButtonEventArgs e)
        {
            listPorts();
            comboBoxPorts.Items.Refresh();
        }
        #endregion

        #region Initialisation and Timer tick handler
        private void receiveTimer_Tick(object sender, EventArgs e)
        {
            printRecvBuf();
        }

        private void init(object sender, RoutedEventArgs e)
        {

            listPorts();

            comboBoxSpeeds.ItemsSource = baudrates;
            comboBoxPorts.ItemsSource = portList;

            // A single tick represents one hundred nanoseconds
            receiveTimer.Interval = new TimeSpan(1000000); // update every millisec            
            receiveTimer.Tick += new EventHandler(receiveTimer_Tick);
        }
        #endregion

    }
}
