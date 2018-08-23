using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using ThreadState = System.Threading.ThreadState;
using Timer = System.Threading.Timer;

namespace GameOfLIfe
{
    public partial class MainForm : Form
    {
        enum enmTeamColor : byte
        {
            Black=0,
            Red = 1,
            Green = 2
        }
        private byte[] CurrentState;
        private byte[] NextState;
        private int width;
        private int height;
        private DistributedThread thrMainThread;
        private Bitmap bmpState;
        private Bitmap bmpCopy;
        private object _lockerObj = new object();
        private Rectangle rect;
        private Stopwatch sw;
        private MethodInfo methodInfo;
        private object oExtLibInstance;
        private bool isMouseDown;
        private Point lastPoint;
        private bool bIsRunnung;
        private delegate void dlgComputingFunction(byte[] curr, byte[] nxt);

        private ManualResetEvent mreStop;
        private dlgComputingFunction dlg;


        private byte[][] colorLUT = new byte[3][];

        

        [DllImport("CPPDLL.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Init(int iw, int ih, byte[] colors);

        [DllImport("CPPDLL.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void CalcNewState(byte[] curr, byte[] nxt);


        public MainForm()
        {


            InitializeComponent();
            InitializeSim();
        }

        private dlgComputingFunction LoadUnMannaged()
        {
            int iProduct = Init(width, height, new byte[]{
                (byte)enmTeamColor.Red,
                (byte)enmTeamColor.Green
            });
            if (iProduct == height * width)
            {
                return CalcNewState;
            }
            else
            {
                return null;
            }
        }

        private dlgComputingFunction LoadMannaged()
        {
            try
            {
                FileInfo dllFile = new FileInfo(@"Engine.dll");
                Assembly engineAssembly = Assembly.LoadFile(dllFile.FullName);
                Type type = engineAssembly.GetType("Engine.Engine");

                //
                // 2. We will be invoking a method: 'public int MyMethod(int count, string text)'
                //
                methodInfo = type.GetMethod("CalcNewState", new Type[] { typeof(byte[]), typeof(byte[]) });
                if (methodInfo == null)
                {
                    // never throw generic Exception - replace this with some other exception type
                    throw new Exception("No such method exists.");
                }

                //
                // 3. Define parameters for class constructor 'MyClass(int initialX, int initialY)'
                //
                oExtLibInstance = Activator.CreateInstance(type, new object[] {width, height});

            }
            catch (Exception ex)
            {

                throw ex;
            }

            return (dlgComputingFunction)Delegate.CreateDelegate(typeof(dlgComputingFunction), oExtLibInstance, methodInfo);
        }

        private void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            // lock (_lockerObj)
            // {
            //     DrawState(CurrentState, ref bmpState);
            //     bmpCopy = bmpState.DeepClone();
            //     pictureBox1.Invoke(new MethodInvoker(() =>
            //     {
            //         pictureBox1.Image = bmpCopy;
            //     }));
            // }
        }

        private void RunSim()
        {

            while (bIsRunnung)
            {
                sw.Restart();

                dlg(CurrentState, NextState);

                //CalcNewState(CurrentState, NextState);

                Buffer.BlockCopy(NextState, 0, CurrentState, 0, width * height); //copy from next to current

                DrawState(CurrentState, ref bmpState);
                //bmpCopy = bmpState.DeepClone();
                bmpCopy = bmpState.Clone(rect, bmpState.PixelFormat);

                double Elapsed = (double)sw.ElapsedTicks / Stopwatch.Frequency * 1000;

                int iRed = 0, iGreen = 0;
                GetScore(ref iRed, ref iGreen, CurrentState);

                pictureBox1.BeginInvoke(new MethodInvoker(() =>
                {
                    pictureBox1.Image = bmpCopy;
                    label1.Text = string.Format("Elapsed: {0:F3} ms", Elapsed);
                    dataGridView1[ScoreCol.Name, 0].Value = iRed;
                    dataGridView1[ScoreCol.Name, 1].Value = iGreen;
                }));

                mreStop.WaitOne();
                //                break;

            }


        }

        private void GetScore(ref int iRed, ref int iGreen, byte[] currentState)
        {
            int iLocGreen = 0, iLocRed = 0;
            Parallel.For(0, currentState.Length, new ParallelOptions() { MaxDegreeOfParallelism = 8 }, (ii, loopstate) =>
              {
                  if (currentState[ii] == (byte)enmTeamColor.Green)
                  {
                      iLocGreen++;
                  }
                  else if (currentState[ii] == (byte)enmTeamColor.Red)
                  {
                      iLocRed++;
                  }
              });

            iRed = iLocRed;
            iGreen = iLocGreen;
        }

        private void DrawState(byte[] stateToDraw, ref Bitmap bmp)
        {
            BitmapData data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);


            for (int i = 0; i < stateToDraw.Length; i++)
            {
                int x = i % width;
                int y = i / width;

                int iKey = stateToDraw[i];

                

                unsafe
                {
                    byte* ptr = (byte*)data.Scan0;
                    ptr[(x * 3) + y * data.Stride + 0] = colorLUT[iKey][2];
                    ptr[(x * 3) + y * data.Stride + 1] = colorLUT[iKey][1];
                    ptr[(x * 3) + y * data.Stride + 2] = colorLUT[iKey][0];

                    //// ARGB case
                    //ptr[(x * 4) + y * data.Stride + 0] = 0;
                    //ptr[(x * 4) + y * data.Stride + 1] = stateToDraw[i];
                    //ptr[(x * 4) + y * data.Stride + 2] = stateToDraw[i];
                    //ptr[(x * 4) + y * data.Stride + 3] = stateToDraw[i];
                }
            }
            bmp.UnlockBits(data);
        }

        private void InitializeSim()
        {
            width = 512;
            height = 512;
            rect = new Rectangle(0, 0, width, height);

            colorLUT[0]= new byte[] { 0, 0, 0 };  //black
            colorLUT[1]= new byte[] { 255, 0, 0 }; //red
            colorLUT[2]= new byte[] { 0, 255, 0 };//green

            bmpState = new Bitmap(width, height);
            CurrentState = new byte[width * height];
            NextState = new byte[width * height];

            sw = new Stopwatch();

            dataGridView1.RowCount = 2;
            dataGridView1[TeamCol.Name, 0].ValueType = typeof(string);
            dataGridView1[TeamCol.Name, 0].Value = "RED";
            dataGridView1[TeamCol.Name, 1].ValueType = typeof(string);
            dataGridView1[TeamCol.Name, 1].Value = "GREEN";

            comboBox1.SelectedIndex = 1;

            InitializeState();

            mreStop = new ManualResetEvent(true);
            bIsRunnung = false;
        }

        private void InitializeState()
        {

            CurrentState[From2DTo1D(0, 0)] = (byte) enmTeamColor.Red;
            CurrentState[From2DTo1D(0, 1)] = (byte)enmTeamColor.Red;
            CurrentState[From2DTo1D(1, 0)] = (byte) enmTeamColor.Red;
            CurrentState[From2DTo1D(1, 2)] = (byte)enmTeamColor.Red;
            // traffic lights
            CurrentState[From2DTo1D(100, 100)] = (byte) enmTeamColor.Green;
            CurrentState[From2DTo1D(100, 102)] = (byte) enmTeamColor.Green;
            CurrentState[From2DTo1D(100, 103)] = (byte) enmTeamColor.Green;
            CurrentState[From2DTo1D(100, 104)] = (byte)enmTeamColor.Green;

            //canoe
            CurrentState[From2DTo1D(199, 49)] =  (byte) enmTeamColor.Red;
            CurrentState[From2DTo1D(199, 50)] =  (byte) enmTeamColor.Red;

            CurrentState[From2DTo1D(200, 50)] =  (byte) enmTeamColor.Red;
            CurrentState[From2DTo1D(201, 49)] =  (byte) enmTeamColor.Red;
            CurrentState[From2DTo1D(202, 48)] =  (byte) enmTeamColor.Red;

            CurrentState[From2DTo1D(202, 47)] =  (byte) enmTeamColor.Red;
            CurrentState[From2DTo1D(202, 46)] =  (byte) enmTeamColor.Red;
            CurrentState[From2DTo1D(201, 46)] = (byte)enmTeamColor.Red;

            //something
            CurrentState[From2DTo1D(300, 49)] =(byte)enmTeamColor.Green; 
            CurrentState[From2DTo1D(301, 48)] =(byte)enmTeamColor.Green; 
            CurrentState[From2DTo1D(300, 47)] =(byte)enmTeamColor.Green; 
                                              
            CurrentState[From2DTo1D(301, 49)] =(byte)enmTeamColor.Green; 
            CurrentState[From2DTo1D(302, 48)] =(byte)enmTeamColor.Green; 
                                          
            CurrentState[From2DTo1D(302, 47)] =(byte)enmTeamColor.Green; 
            CurrentState[From2DTo1D(302, 46)] =(byte)enmTeamColor.Green; 
            CurrentState[From2DTo1D(301, 46)] = (byte)enmTeamColor.Green;
        }

        private int From2DTo1D(int x, int y)
        {
            return y * width + x;
        }

        private void From1DTo2D(int i, Point p)
        {

            p.X = i / width;
            p.Y = i % width;

        }


        private void button1_Click(object sender, EventArgs e)
        {
            if (!bIsRunnung)
            {
                InitializeState();
                dlg = ChooseDelegate();
                bIsRunnung = true;
                thrMainThread = new DistributedThread(RunSim);
                thrMainThread.ManagedThread.IsBackground = true;
                thrMainThread.ProcessorAffinity = 1 << 1;

                thrMainThread.Start();
                button1.Text = "Stop";

            }
            else
            {
                bIsRunnung = false;
                thrMainThread.ManagedThread.Join();
                button1.Text = "Run";
            }

            //System.Timers.Timer timer = new System.Timers.Timer(20);
            // timer.Elapsed += TimerOnElapsed;
            //timer.Start();
        }
        private dlgComputingFunction ChooseDelegate()
        {
            dlgComputingFunction dlgComputingFunction;
            if (int.Parse(comboBox1.SelectedIndex.ToString()) == 0)
            {
                dlgComputingFunction = LoadUnMannaged();
            }
            else
            {
                dlgComputingFunction = LoadMannaged();
            }
            return dlgComputingFunction;
        }


        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)

        {
            lastPoint = e.Location;
            isMouseDown = true;
            mreStop.Reset();

        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)

        {

            if (isMouseDown)//check to see if the mouse button is down

            {
                //if (lastPoint != null)//if our last point is not null, which in this case we have assigned above
                SizeF scaleFactor = GetScaleFactor(pictureBox1);
                PointF newP = new PointF(e.X / scaleFactor.Width, e.Y / scaleFactor.Height);
                int iIndx = From2DTo1D((int)newP.X, (int)newP.Y);
                if ((e.Button & MouseButtons.Left) != 0)
                {
                    CurrentState[iIndx] = (byte)enmTeamColor.Red;
                }
                else
                {
                    CurrentState[iIndx] = (byte)enmTeamColor.Green;
                }


                lastPoint = new Point((int)newP.X, (int)newP.Y);//keep assigning the lastPoint to the current mouse position

            }

        }

        private SizeF GetScaleFactor(PictureBox pBox)
        {
            return new SizeF((float)pBox.Size.Width / pBox.Image.Width, (float)pBox.Size.Height / pBox.Image.Height);
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)

        {
            isMouseDown = false;
            lastPoint = Point.Empty;
            mreStop.Set();
            //set the previous point back to null if the user lets go of the mouse button
        }

    }

    public static class ObjectCopier
    {
        /// <summary>
        /// Perform a deep Copy of the object.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>The copied object.</returns>
        public static T DeepClone<T>(this T source)
        {
            if (!typeof(T).IsSerializable)
            {
                throw new ArgumentException(@"The type must be serializable.", "source");
            }

            // Don't serialize a null object, simply return the default for that object
            if (Object.ReferenceEquals(source, null))
            {
                return default(T);
            }

            IFormatter formatter = new BinaryFormatter();
            Stream stream = new MemoryStream();
            using (stream)
            {
                formatter.Serialize(stream, source);
                stream.Seek(0, SeekOrigin.Begin);
                return (T)formatter.Deserialize(stream);
            }
        }
    }
}
