using UnityEngine;
using System.Collections;
using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

public class SpeechRecognition : MonoBehaviour
{
    public Thread receiveThread; // A thread to call ReceiveData function 
    public UdpClient client; //udpclient for receiving udp msgs from SpeechRecognition.exe
    public int port = 9091; // the communication port with the external process
    public Process foo; // represents SpeechRecognition.exe

    public void Start()
    {
        //runs the speech recognition exe - will pass the recognized words by udp packets
        foo = new Process();
        foo.StartInfo.FileName = @"C:\Users\win7\Documents\Visual Studio 2010\Projects\SpeechRecognition\SpeechRecognition\bin\Debug\SpeechRecognition.exe";
        //hides the process in windows bar
        foo.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        foo.Start();
        //initializes & starts the receiveThread - responsible for udp msgs
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }
    /// <summary>
    /// ReceiveData function receives a string via udp represents a recognized word/s
    /// Bytes are sent from SpeechRecognition.exe only when it successfully recognized word/s
    /// </summary>
    private void ReceiveData()
    {
        // creates a udpclient with the specific port to communicate with specch recognition exe
        client = new UdpClient(port);
        while (true)
        {
            try
            {
                // receive bytes only when a word was recognized by the exe
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);
                // prints the recognized word/s
                print(Encoding.ASCII.GetString(data, 0, data.Length));
            }
            catch (Exception err)
            {
                print(err.ToString());
            }
        }
    }
    /// <summary>
    /// This function is sent to all game objects before the application is quit.
    /// </summary>
    void OnApplicationQuit()
    {
        //terminates the thread used for ReceiveData function
        if (receiveThread != null) receiveThread.Abort();
        // closes the udp client if its not null
        client.Close();
        // kills the speech recognition process
        foo.Kill();
    }
}
