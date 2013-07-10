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
using System.ComponentModel;
using System.Diagnostics;
using System.Management;

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
        List<string> baudrates = new List<string>(){"1200", "2400", "4800", "9600",
                                                    "14400", "19200", "28800", "38400",
                                                    "56000", "57600", "115200", "256000"};
        List<string> portList = new List<string>();
        ConcurrentQueue<char> receiveQueue = new ConcurrentQueue<char>();
        DispatcherTimer receiveTimer = new System.Windows.Threading.DispatcherTimer();
        ManagementEventWatcher watcher;
        Boolean portOpen = false;
        char lastchar;

        #endregion

        #region Serial Port stuff
        void listPorts()
        {
            portList.Clear();
            foreach (string port in SerialPort.GetPortNames())
            {
                portList.Add(port);
            }
        }

        void sendData(String str)
        {
            if (portOpen)
            {
                try
                {
                    serialport.Write(str);
                }
                catch (Exception ex)
                {
                    addTextError(ex.Message + "\n");
                }
            }
            else
            {
                addTextError("Not connected\n");
            }

        }

        void readSerialBuffer()
        {
            int buflen = serialport.BytesToRead;
            char[] recBuf = new char[buflen];

            try
            {
                serialport.Read(recBuf, 0, buflen);
                for (int index = 0; index < buflen; index++)
                {
                    lastchar = recBuf[index];
                    addTextReceived(lastchar.ToString());
                }
            }
            catch (Exception ex)
            {
                addTextError(ex.Message + "\n");
            }
        }

        void receiveData(object sender, SerialDataReceivedEventArgs ev)
        {
            readSerialBuffer();
        }

        #endregion

        #region Terminal text functions
        void addText(String text, Brush color)
        {
            // Use dispatcher invoke so this could be used from other threads as well
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    //char c = txtTerminal.Text[txtTerminal.Text.Length - 1];
                    //string lastChar = txtTerminal.Substring(txtTerminal.length - 1)

                    TextRange tr = new TextRange(txtTerminal.Document.ContentEnd, txtTerminal.Document.ContentEnd);
                    tr.Text = text;
                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, color);
                    //tr.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);

                    if (chkBoxScroll.IsChecked == true)
                    {
                        txtTerminal.ScrollToEnd();
                    }
                }), DispatcherPriority.ApplicationIdle);
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
            addText(addNewline(text), Brushes.Red);
        }

        void addTextInfo(String text)
        {
            addText(addNewline(text), Brushes.LightSkyBlue);
        }

        string addNewline(String text)
        {
            // this is quite hacky:
            // check if the existing text already has newlines so that appended messages
            // will be added on the next line. RichTextBox control seems to add \r\n by
            // default it seems, which is annoying...
            string txtTerminalText = "";

            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    txtTerminal.SelectAll();
                    txtTerminalText = txtTerminal.Selection.Text;
                }));

            if (txtTerminalText.Length > 4)
            {
                string l = txtTerminalText.Substring(txtTerminalText.Length - 4);
                if (String.Compare(l, "\r\n\r\n") != 0)
                {
                    text = "\n" + text;
                }
            }
            

            return text;
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

        #region Open/Close button
        private void btnControl_Click(object sender, RoutedEventArgs e)
        {

            if (portOpen)
            {
                try
                {
                    serialport.Close();
                    portOpen = false;

                    btnControl.Content = "Open Port";
                    addTextInfo(comboBoxPorts.SelectedItem + " closed\n");
                    comboBoxPorts.IsEnabled = true;
                    comboBoxSpeeds.IsEnabled = true;
                    windowMain.Title = "okterm";
                }
                catch (Exception ex)
                {
                    addTextError(ex.Message + "\n");
                }

            }
            else
            {
                if ((comboBoxSpeeds.SelectedItem == null) || (comboBoxPorts.SelectedItem == null))
                {
                    addTextError("select port/speed");
                }
                else
                {

                    try
                    {
                        serialport.PortName = comboBoxPorts.SelectedItem.ToString();
                        serialport.BaudRate = int.Parse(comboBoxSpeeds.SelectedItem.ToString());
                        serialport.Open();
                        portOpen = true;

                        serialport.DiscardInBuffer();
                        serialport.DiscardOutBuffer();
                        serialport.DataReceived += new SerialDataReceivedEventHandler(receiveData);

                        btnControl.Content = "Close Port";
                        addTextInfo(comboBoxPorts.SelectedItem + " opened\n");
                        comboBoxPorts.IsEnabled = false;
                        comboBoxSpeeds.IsEnabled = false;
                        windowMain.Title = "okterm [" + comboBoxPorts.SelectedItem + ", " + comboBoxSpeeds.SelectedItem + "]";

                        //readSerialBuffer();
                    }
                    catch (Exception ex)
                    {
                        addTextError(ex.Message + "\n");
                    }
                }
            }
        }


        #endregion

        #region App event handlers
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // to prevent "COM object that has been separated from its underlying RCW cannot be used."
            watcher.Stop();
        }

        private void handleWMIEvent(object sender, EventArrivedEventArgs e)
        {
            int EventType = int.Parse(e.NewEvent.GetPropertyValue("EventType").ToString());

            //event types: http://msdn.microsoft.com/en-us/library/aa394124%28VS.85%29.aspx
            Console.Write("Win32_DeviceChangeEvent: ");
            switch (EventType)
            {
                case 1:
                    addTextInfo("Configuration changed\n");
                    break;

                case 2:
                    addTextInfo("Device Arrival\n");
                    break;

                case 3:
                    addTextInfo("Device Removal\n");
                    break;

                case 4:
                    addTextInfo("Docking\n");
                    break;

                default:
                    addTextInfo("Unknown event type!\n");
                    break;
            }

            listPorts();
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    comboBoxPorts.Items.Refresh();
                }), DispatcherPriority.ApplicationIdle);

        }

        #endregion

        #region UI Event Handlers
        private void updateTerminalScroll(object sender, SizeChangedEventArgs e)
        {
            if (chkBoxScroll.IsChecked == true)
            {
                txtTerminal.ScrollToEnd();
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

        #region Initialisation
        private void init(object sender, RoutedEventArgs e)
        {

            listPorts();

            comboBoxSpeeds.ItemsSource = baudrates;
            comboBoxPorts.ItemsSource = portList;

            try
            {
                WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent");
                watcher = new ManagementEventWatcher(query);
                watcher.EventArrived += new EventArrivedEventHandler(handleWMIEvent);
                watcher.Start();
            }
            catch (ManagementException ex)
            {
                addTextError(ex.Message + "\n");
            }

            txtPrompt.Focus();

        }

        #endregion



    }
}
