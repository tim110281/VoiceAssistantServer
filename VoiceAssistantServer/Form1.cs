using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;

namespace VoiceAssistantServer
{
    public partial class Form1 : Form
    {
        public class Patient
        {
            public String chart_no;
            public String name;
            public DateTime date;
            public TimeSpan time;
            public byte bp_systolic;
            public byte bp_diastolic;
            public float temperature;
            public byte pulse;
            public byte respire;
        }
        public class Nurse
        {
            public int nurseNo;
            public String name;
            public DateTime loginDate;
            public TimeSpan loginTime;
            public DateTime logoutDate;
            public TimeSpan logoutTime;
        }
        public class StateObject
        {
            // Client  socket.  
            public Socket workSocket = null;
            // Size of receive buffer.  
            public const int BufferSize = 1024;
            // Receive buffer.  
            public byte[] buffer = new byte[BufferSize];
            // Received data string.  
            public StringBuilder sb = new StringBuilder();
        }
        // Thread signal.  
        public ManualResetEvent allDone = new ManualResetEvent(false);

        // declare a global array that store all the nurses which have already logged
        public List<Nurse> nurseArray = new List<Nurse>();

        public void display(String text)
        {
            label1.Text = text;
        }

        public Form1()
        {
            InitializeComponent();
        }

        // ================== All the Async TCP Socket Function(begin) ================== //

        // 1. StartListening
        public void StartListening()
        {
            // Establish the local endpoint for the socket.  
            // The DNS name of the computer  
            // running the listener is "host.contoso.com".  
            //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            //IPAddress ipAddress = ipHostInfo.AddressList[2];
            IPAddress ipAddress = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            // Create a TCP/IP socket.  
            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(5);

                while (true)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    // Wait until a connection is made before continuing.  
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

        //2. AcceptCallback, will be automatically called when server accepts a client socket
        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            allDone.Set();

            // Get the socket that handles the client request.  
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object.  
            StateObject state = new StateObject();
            state.workSocket = handler;

            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        //3. ReadCallback, will be automatically called when client sends a message to server
        public void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;
            JObject jsonObject;

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                content = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                //display(content);

                jsonObject = JObject.Parse(content);

                parseCommand(jsonObject);

                /*// There  might be more data, so store the data received so far.  
                state.sb.Append(Encoding.ASCII.GetString(
                    state.buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read
                // more data.  
                content = state.sb.ToString();
                if (content.IndexOf("<EOF>") > -1)
                {
                    // All the data has been read from the
                    // client. Display it on the console.  
                    JObject jObject = JObject.Parse(content);
                    
                }
                else
                {
                    // Not all data received. Get more.  
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }*/
            }
            else
            {
                // if bytesRead is negative then close the socket
                handler.Close();
            }
        }

        //4. 
        private void Send(Socket handler, String data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.  
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }

        //5. SendCallback, will be automatically called when server sends a message to client successfully.
        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        // ================== All the Async TCP Socket Function(end) ================== //


        private void updateDatabase(Nurse nurse, Patient patient)
        {
            String con = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\waitinglin\source\repos\VoiceAssistantServer\VoiceAssistantServer\Database1.mdf;Integrated Security=True";
            SqlConnection connection = new SqlConnection(con);

          

            String sql;
            if(nurse != null)
            {
                sql = "INSERT INTO Nurse ([NurseNo], [Name], [LoginDate], [LoginTime], [LogoutDate], [LogoutTime]) VALUES(@NurseNo, @Name, @LoginDate, @LoginTime, @LogoutDate, @LogoutTime);";

                using (SqlCommand sqlCommand = new SqlCommand(sql, connection))
                {
                    sqlCommand.CommandType = CommandType.Text;

                    sqlCommand.Parameters.AddWithValue("@NurseNo", nurse.nurseNo);
                    sqlCommand.Parameters.AddWithValue("@Name", nurse.name);
                    sqlCommand.Parameters.AddWithValue("@LoginDate", nurse.loginDate);
                    sqlCommand.Parameters.AddWithValue("@LoginTime", nurse.loginTime);
                    sqlCommand.Parameters.AddWithValue("@LogoutDate", nurse.logoutDate);
                    sqlCommand.Parameters.AddWithValue("@LogoutTime", nurse.logoutTime);

                    connection.Open();
                    sqlCommand.ExecuteNonQuery();
                }

                connection.Close();

            }

            if(patient != null)
            {
                sql = "INSERT INTO Patient ([CHART_NO], [Name], [DATE], [TIME], [BP_SYSTOLIC], [BP_DIASTOLIC], [TEMPERATURE], [PULSE], [RESPIRE]) VALUES(@CHART_NO, @Name, @DATE, @TIME, @BP_SYSTOLIC, @BP_DIASTOLIC, @TEMPERATURE, @PULSE, @RESPIRE);";
                using (SqlCommand sqlCommand = new SqlCommand(sql, connection))
                {
                    sqlCommand.CommandType = CommandType.Text;

                    sqlCommand.Parameters.AddWithValue("@CHART_NO", patient.chart_no);
                    sqlCommand.Parameters.AddWithValue("@Name", patient.name);
                    sqlCommand.Parameters.AddWithValue("@DATE", patient.date);
                    sqlCommand.Parameters.AddWithValue("@TIME", patient.time);
                    sqlCommand.Parameters.AddWithValue("@BP_SYSTOLIC", patient.bp_systolic);
                    sqlCommand.Parameters.AddWithValue("@BP_DIASTOLIC", patient.bp_diastolic);
                    sqlCommand.Parameters.AddWithValue("@TEMPERATURE", patient.temperature);
                    sqlCommand.Parameters.AddWithValue("@PULSE", patient.pulse);
                    sqlCommand.Parameters.AddWithValue("@RESPIRE", patient.respire);


                    connection.Open();
                    sqlCommand.ExecuteNonQuery();
                }

                connection.Close();
            }
        }

        private void parseCommand(JObject jsonObject)
        {
            String command = jsonObject["Command"].Value<String>();
            if(command == "Login")
            {
                DateTime date = DateTime.Now.Date;
                TimeSpan time = DateTime.Now.TimeOfDay;
                Nurse nurse = new Nurse();
                nurse.name = jsonObject["Name"].Value<String>();
                nurse.nurseNo = jsonObject["NurseNo"].Value<int>();
                nurse.loginDate = date;
                nurse.loginTime = time;
                nurse.logoutDate = date;
                nurse.logoutTime = time;

                nurseArray.Add(nurse);

                updateDatabase(nurse, null);
            }
            else if(command == "Logout")
            {
                int index = nurseArray.FindIndex(n => n.name == jsonObject["Name"].Value<String>());
                if (index < 0)
                    return;

                DateTime date = DateTime.Today.Date;
                TimeSpan time = DateTime.Now.TimeOfDay;
            
                
                nurseArray[index].logoutDate = date;
                nurseArray[index].logoutTime = time;

                updateDatabase(nurseArray[index], null);

                nurseArray.RemoveAt(index);
            }
            else if(command == "record")
            {

            }

        }

        // =================== Button OnClick Function (begin) =================== //
        private void button1_Click(object sender, EventArgs e)
        {
            String con = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\waitinglin\source\repos\VoiceAssistantServer\VoiceAssistantServer\Database1.mdf;Integrated Security=True";
            SqlConnection connection = new SqlConnection(con);
            String sql = "INSERT INTO Patient ([Name], [DATE], [TIME], [BP_SYSTOLIC], [TEMPERATURE]) VALUES(@Name, @DATE, @TIME, @BP_SYSTOLIC, @TEMPERATURE);";

            DateTime date = DateTime.Today.Date;
            TimeSpan time = DateTime.Now.TimeOfDay;
            using (SqlCommand sqlCommand = new SqlCommand(sql, connection))
            {
                sqlCommand.CommandType = CommandType.Text;

                sqlCommand.Parameters.AddWithValue("@Name", "Tim");
                sqlCommand.Parameters.AddWithValue("@DATE", date);
                sqlCommand.Parameters.AddWithValue("@TIME", time);
                sqlCommand.Parameters.AddWithValue("@BP_SYSTOLIC", 138);
                sqlCommand.Parameters.AddWithValue("@TEMPERATURE", 36.7);

                connection.Open();
                sqlCommand.ExecuteNonQuery();
            }

            connection.Close();
        }
        //show the databse's data on the dataGridView
        private void button2_Click(object sender, EventArgs e)
        {
            String con, sql;
            con = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\waitinglin\source\repos\VoiceAssistantServer\VoiceAssistantServer\Database1.mdf;Integrated Security=True";
            sql = "select * from Patient";
            SqlConnection connection = new SqlConnection(con);
            connection.Open();
            SqlDataAdapter dataAdapter = new SqlDataAdapter(sql, con);
            DataSet dataSet = new DataSet();
            dataAdapter.Fill(dataSet, "patient");
            dataGridView1.DataSource = dataSet.Tables["patient"];
            connection.Close();
        }
        private void button3_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(StartListening);
            thread.Start();
        }

        // =================== Button OnClick Function (end) =================== //
    }
}
