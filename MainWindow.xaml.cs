using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Net;
using System.IO;
using System.Timers;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Threading;

namespace KiepTeletext
{
    public partial class MainWindow : Window
    {
        private const int KEY_SCANNING = 107;
        private const int KEY_SAY = 111;
        private const int KEY_YES = 109;
        private const int KEY_NO = 106;
        private const int KEY_ESCAPE = 27;
        private const int KEY_ENTER = 13;
        private const int KEY_SPACE = 32;
        private const int KEY_LEFT = 37;
        private const int KEY_RIGHT = 39;
        private const int KEY_UP = 38;
        private const int KEY_DOWN = 40;
        private const int KEY_TAB = 9;
        private const int KEY_PAGEUP = 33;
        private const int KEY_PAGEDOWN = 34;

        private const string LOGFILE = "KiepTeletext.log";
        private const string URL = "http://nos.nl/data/teletekst/gif/P{0}_{1}.gif";
        private const int DEFAULT_PAGE = 100;
        private const int DEFAULT_SUBPAGE = 1;

        private int pageSelected = DEFAULT_PAGE;
        private int subPageSelected = DEFAULT_SUBPAGE;

        private bool keyNoOrMouseRightPressed = false;
        private bool keyYesOrMouseLeftPressed = false;

        private ScanningFunctions currentFunction = ScanningFunctions.NoZoom;
       
        private delegate void DummyDelegate();

        private enum ScanningFunctions
        {
            NoZoom,
            ZoomTopLeft,
            ZoomTopRight,
            ZoomBottomLeft,
            ZoomBottomRight,
            Quit
        }

        public MainWindow()
        {
            InitializeComponent();

            // Cannot debug when application has topmost
#if !DEBUG
            this.Topmost = true;
#endif

            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform());
            transformGroup.Children.Add(new TranslateTransform());
            imgTeletext.RenderTransform = transformGroup;

            // Wait 1 second before subscribing to keypresses
            WaitSubscribeKeypresses();

            // Log startup
            Log("Start");

            // Create URL from command line
            ReadCommandline();

            // Initialize
            DownloadPage();
        }

        void DownloadPage()
        {
            // Add extra zero to subpage if needed
            string subpage = subPageSelected.ToString();
            if (subpage.Length == 1)
            {
                subpage = "0" + subpage;
            }

            // Construct URL
            string url = String.Format(URL, pageSelected, subpage);

            // Use rotating icon
            imgStatus.Source = new BitmapImage(new Uri("Sync.png", UriKind.Relative));

            // Apply rotation animation to status image
            DoubleAnimation da = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(3)));
            RotateTransform rt = new RotateTransform();
            imgStatus.RenderTransform = rt;
            imgStatus.RenderTransformOrigin = new Point(0.5, 0.5);
            da.RepeatBehavior = RepeatBehavior.Forever;
            rt.BeginAnimation(RotateTransform.AngleProperty, da);

            // Show image
            TryReadFromWeb(url);
        }

        private void ReadCommandline()
        {
#if !DEBUG
            try
            {
#endif
                string[] commandLineArgs = Environment.GetCommandLineArgs();
                for (int i = 0; i < commandLineArgs.Length; i++)
                {
                    switch (commandLineArgs[i])
                    {
                        case "-pagina":
                        case "-page":
                            i++;
                            if (commandLineArgs.Length > i)
                            {
                                try
                                {
                                    pageSelected = int.Parse(commandLineArgs[i]);
                                }
                                catch (Exception) { }
                            }
                            break;

                        case "-subpagina":
                        case "-subpage":
                            i++;
                            if (commandLineArgs.Length > i)
                            {
                                try
                                {
                                    subPageSelected = int.Parse(commandLineArgs[i]);
                                }
                                catch (Exception) { }
                            }
                            break;

                        default:
                            // Unknown argument: do nothing
                            break;
                    }
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                Log("Error reading command line\t" + ex.Message);
            }
#endif
        }

        #region Keyboard and mouse handling
        private void WaitSubscribeKeypresses()
        {
#if !DEBUG
            try
            {
#endif
                Timer timer = new Timer(1000);
                timer.Elapsed += delegate
                {
                    this.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    (DummyDelegate)
                    delegate 
                    {
                        timer.Enabled = false;

                        // Block keys from being received by other applications
                        List<int> blockedKeys = new List<int>(2);
                        blockedKeys.Add(KEY_YES);
                        blockedKeys.Add(KEY_NO);
                        blockedKeys.Add(KEY_SAY);
                        LowLevelKeyboardHook.Instance.SetBlockedKeys(blockedKeys);

                        // Subscribe to low level keypress events
                        LowLevelKeyboardHook.Instance.KeyDownEvent += LowLevelKeyDownEvent;
                        LowLevelKeyboardHook.Instance.KeyUpEvent += LowLevelKeyUpEvent;
                    });
                };
                timer.Enabled = true;
#if !DEBUG
            }
            catch (Exception ex)
            {
                Log("Error attaching keyboard hook\t" + ex.Message);
            }
#endif
        }

        void LowLevelKeyDownEvent(int keycode)
        {
            Console.WriteLine("Keycode: {0}", keycode);
            Log("Keypress\t" + keycode);
            switch (keycode)
            {
                case KEY_ESCAPE:
                case KEY_ENTER:
                case KEY_SPACE:
                    Log("Keypress\tESCAPE/ENTER/SPACE\tExit");
                    Application.Current.Shutdown();
                    break;
                case KEY_YES:
                    keyYesOrMouseLeftPressed = true;

                    switch (currentFunction)
                    {
                        case ScanningFunctions.ZoomTopLeft:
                            Log("Keypress\tYES\tZoomTopRight");
                            currentFunction = ScanningFunctions.ZoomTopRight;
                            break;
                        default:
                            Log("Keypress\tYES\tZoomTopLeft");
                            currentFunction = ScanningFunctions.ZoomTopLeft;
                            break;
                    }
                    ApplyZoom();
                    break;
                case KEY_NO:
                    keyNoOrMouseRightPressed = true;

                    switch (currentFunction)
                    {
                        case ScanningFunctions.ZoomBottomLeft:
                            Log("Keypress\tNO\tZoomBottomRight");
                            currentFunction = ScanningFunctions.ZoomBottomRight;
                            break;
                        default:
                            Log("Keypress\tNO\tZoomBottomLeft");
                            currentFunction = ScanningFunctions.ZoomBottomLeft;
                            break;
                    }
                    ApplyZoom();
                    break;
                case KEY_SAY:
                    if (currentFunction == ScanningFunctions.NoZoom)
                    {
                        Log("Keypress\tSAY\tNextSubpage");
                        // Go to next subpage and reload
                        subPageSelected++;
                        DownloadPage();
                    }
                    else
                    {
                        Log("Keypress\tSAY\tNoZoom");
                        currentFunction = ScanningFunctions.NoZoom;
                        ApplyZoom();
                    }
                    break;
                case KEY_LEFT:
                    switch (currentFunction)
                    {
                        case ScanningFunctions.NoZoom:
                            Log("Keypress\tLEFT\tZoomTopLeft");
                            currentFunction = ScanningFunctions.ZoomTopLeft;
                            break;
                        case ScanningFunctions.ZoomTopRight:
                            Log("Keypress\tLEFT\tZoomTopLeft");
                            currentFunction = ScanningFunctions.ZoomTopLeft;
                            break;
                        case ScanningFunctions.ZoomBottomRight:
                            Log("Keypress\tLEFT\tZoomBottomLeft");
                            currentFunction = ScanningFunctions.ZoomBottomLeft;
                            break;
                        default:
                            break;
                    }
                    ApplyZoom();
                    break;
                case KEY_RIGHT:
                    switch (currentFunction)
                    {
                        case ScanningFunctions.NoZoom:
                            Log("Keypress\tRIGHT\tZoomTopRight");
                            currentFunction = ScanningFunctions.ZoomTopRight;
                            break;
                        case ScanningFunctions.ZoomTopLeft:
                            Log("Keypress\tRIGHT\tZoomTopRight");
                            currentFunction = ScanningFunctions.ZoomTopRight;
                            break;
                        case ScanningFunctions.ZoomBottomLeft:
                            Log("Keypress\tRIGHT\tZoomBottomRight");
                            currentFunction = ScanningFunctions.ZoomBottomRight;
                            break;
                        default:
                            break;
                    }
                    ApplyZoom();
                    break;
                case KEY_UP:
                    switch (currentFunction)
                    {
                        case ScanningFunctions.NoZoom:
                            Log("Keypress\tUP\tZoomTopLeft");
                            currentFunction = ScanningFunctions.ZoomTopLeft;
                            break;
                        case ScanningFunctions.ZoomBottomLeft:
                            Log("Keypress\tUP\tZoomTopLeft");
                            currentFunction = ScanningFunctions.ZoomTopLeft;
                            break;
                        case ScanningFunctions.ZoomBottomRight:
                            Log("Keypress\tUP\tZoomTopRight");
                            currentFunction = ScanningFunctions.ZoomTopRight;
                            break;
                        default:
                            break;
                    }
                    ApplyZoom();
                    break;
                case KEY_DOWN:
                    Log("Keypress\tDOWN");
                    switch (currentFunction)
                    {
                        case ScanningFunctions.NoZoom:
                            Log("Keypress\tDOWN\tZoomBottomLeft");
                            currentFunction = ScanningFunctions.ZoomBottomLeft;
                            break;
                        case ScanningFunctions.ZoomTopLeft:
                            Log("Keypress\tDOWN\tZoomBottomLeft");
                            currentFunction = ScanningFunctions.ZoomBottomLeft;
                            break;
                        case ScanningFunctions.ZoomTopRight:
                            Log("Keypress\tDOWN\tZoomBottomRight");
                            currentFunction = ScanningFunctions.ZoomBottomRight;
                            break;
                        default:
                            break;
                    }
                    ApplyZoom();
                    break;
                case KEY_TAB:
                    Log("Keypress\tTAB\tNoZoom");
                    currentFunction = ScanningFunctions.NoZoom;
                    ApplyZoom();
                    break;
                case KEY_PAGEUP:
                    Log("Keypress\tPAGEUP\tPreviousPage");
                    pageSelected--;
                    subPageSelected = DEFAULT_SUBPAGE;
                    DownloadPage();
                    break;
                case KEY_PAGEDOWN:
                    Log("Keypress\tPAGEDOWN\tNextPage");
                    subPageSelected++;
                    DownloadPage();
                    break;
                default:
                    if (keycode >= 48 && keycode <= 57)
                    {
                        string keyString = ((char)keycode).ToString();
                        ShowNumKeyPress(keyString);
                    }
                    break;
            }
        }

        void ShowNumKeyPress(string keyString)
        {
            Log("Keypress\t" + keyString);
            tbPageSelection.Visibility = System.Windows.Visibility.Visible;

            // Reset subpage selection
            subPageSelected = DEFAULT_SUBPAGE;

            string pageSelectedString = tbPageSelection.Text + keyString;
            if (pageSelectedString.Length > 3)
            {
                pageSelectedString = pageSelectedString.Substring(1, 3);
            }

            tbPageSelection.Text = pageSelectedString;

            if (pageSelectedString.Length == 3)
            {
                try
                {
                    pageSelected = int.Parse(pageSelectedString);
                    DownloadPage();
                }
                catch (Exception ex)
                {
                    Log("Handling keypress\t" + ex.Message);
                }
            }

        }

        void LowLevelKeyUpEvent(int keycode)
        {
            // Exit when both Scanning and Say are pressed simultanuously
            if (keyYesOrMouseLeftPressed && keyNoOrMouseRightPressed)
            {
                // Disabled because Tobii has to send quit
                //Application.Current.Shutdown();
            }

            switch (keycode)
            {
                case KEY_YES:
                    keyYesOrMouseLeftPressed = false;
                    break;
                case KEY_NO:
                    keyNoOrMouseRightPressed = false;
                    break;
                default:
                    break;
            }
        }

        private void MouseDownHandler(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                Log("MouseClick\tLEFT");
                keyYesOrMouseLeftPressed = true;

                switch (currentFunction)
                {
                    case ScanningFunctions.ZoomTopLeft:
                        Log("MouseClick\tLEFT\tZoomTopRight");
                        currentFunction = ScanningFunctions.ZoomTopRight;
                        break;
                    default:
                        Log("MouseClick\tLEFT\tZoomTopLeft");
                        currentFunction = ScanningFunctions.ZoomTopLeft;
                        break;
                }
                ApplyZoom();
            }
            else
            {
                Log("MouseClick\tRIGHT");
                keyNoOrMouseRightPressed = true;

                switch (currentFunction)
                {
                    case ScanningFunctions.ZoomBottomLeft:
                        Log("MouseClick\tRIGHT\tZoomBottomRight");
                        currentFunction = ScanningFunctions.ZoomBottomRight;
                        break;
                    default:
                        Log("MouseClick\tRIGHT\tZoomBottomLeft");
                        currentFunction = ScanningFunctions.ZoomBottomLeft;
                        break;
                }
                ApplyZoom();
            }

            // Both Left and Right mouse button are pressed simultanuously
            if (keyYesOrMouseLeftPressed && keyNoOrMouseRightPressed)
            {
                Log("MouseClick\tBOTH\tExit");
                Application.Current.Shutdown();
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                keyYesOrMouseLeftPressed = false;
            }
            else
            {
                keyNoOrMouseRightPressed = false;
            }
        }

        private void ApplyZoom()
        {
            TransformGroup transformGroup = (TransformGroup)imgTeletext.RenderTransform;
            ScaleTransform scaleTransform = (ScaleTransform)transformGroup.Children[0];
            TranslateTransform translateTransform = (TranslateTransform)transformGroup.Children[1];
            switch (currentFunction)
            {
                case ScanningFunctions.NoZoom:
                    scaleTransform.ScaleX = 1;
                    scaleTransform.ScaleY = 1;
                    translateTransform.X = 0;
                    translateTransform.Y = 0;
                    break;
                case ScanningFunctions.ZoomTopLeft:
                    scaleTransform.ScaleX = 2;
                    scaleTransform.ScaleY = 2;
                    translateTransform.X = 0;
                    translateTransform.Y = 0;
                    break;
                case ScanningFunctions.ZoomTopRight:
                    scaleTransform.ScaleX = 2;
                    scaleTransform.ScaleY = 2;
                    translateTransform.X = -imgTeletext.ActualWidth;
                    translateTransform.Y = 0;
                    break;
                case ScanningFunctions.ZoomBottomLeft:
                    scaleTransform.ScaleX = 2;
                    scaleTransform.ScaleY = 2;
                    translateTransform.X = 0;
                    translateTransform.Y = -imgTeletext.ActualHeight;
                    break;
                case ScanningFunctions.ZoomBottomRight:
                    scaleTransform.ScaleX = 2;
                    scaleTransform.ScaleY = 2;
                    translateTransform.X = -imgTeletext.ActualWidth;
                    translateTransform.Y = -imgTeletext.ActualHeight;
                    break;
                case ScanningFunctions.Quit:
                    Application.Current.Shutdown();
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region Read from web (asynchronous)
        private void TryReadFromWeb(string url)
        {
#if !DEBUG
            try
            {
#endif
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += new DoWorkEventHandler(bw_DoWork);
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
                bw.RunWorkerAsync(url);
#if !DEBUG
            }
            catch (Exception ex)
            {
                Log("Error running download thread\t" + ex.Message);
            }
#endif
        }

        void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = null;
            try
            {
                string url = (string)e.Argument;
                Console.WriteLine("URL: " + url);
                WebClient webClient = new WebClient();
                byte[] downloadedImage = webClient.DownloadData(url);
                e.Result = downloadedImage;
            }
            catch (Exception ex)
            {
                Log("Error downloading URL\t" + ex.Message);
            }
        }

        void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            byte[] result = (byte[])e.Result;
            if (result != null && result.Length > 0)
            {
                ShowImageFromBuffer(result);
                imgStatus.Source = null;
                imgStatus.RenderTransform = null;
                tbPageSelection.Text = "";
                tbPageSelection.Visibility = System.Windows.Visibility.Hidden;
                Log("Loaded\t" + pageSelected + " - " + subPageSelected);
            }
            else
            {
                if (subPageSelected > DEFAULT_SUBPAGE)
                {
                    // Navigated to a non-existing sub page -> go to first sub page and reload
                    subPageSelected = DEFAULT_SUBPAGE;
                    pageSelected++;
                    DownloadPage();
                }
                else
                {
                    tbPageSelection.Text = "";
                    imgStatus.Source = new BitmapImage(new Uri("Error.png", UriKind.Relative));
                    imgStatus.RenderTransform = null;
                    Log("Error loading\t" + pageSelected + " - " + subPageSelected);
                    //lblError.Visibility = System.Windows.Visibility.Visible;
                }
            }
        }
        #endregion

        #region Show image
        private void ShowImageFromBuffer(byte[] downloadedImage)
        {
#if !DEBUG
            try
            {
#endif
                if (downloadedImage != null && downloadedImage.Length > 0)
                {
                    // Reset transformations
                    currentFunction = ScanningFunctions.NoZoom;
                    ApplyZoom();

                    MemoryStream stream = new MemoryStream(downloadedImage);
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = stream;
                    image.EndInit();
                    imgTeletext.Source = image;
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                Log("Error showing image\t" + ex.Message);
            }
#endif
        }
        #endregion

        private void Log(string text)
        {
            try
            {
                if (text != "")
                {
                    string baseDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
                    StreamWriter cachefile = File.AppendText(baseDir + "\\" + LOGFILE);
                    cachefile.WriteLine(DateTime.Now + "\t" + text);
                    cachefile.Close();
                }
            }
            catch (Exception) { }
        }
    }
}
