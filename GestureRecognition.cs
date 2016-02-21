using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
public class GestureRecognition : MonoBehaviour
{
    public UdpClient client;
    public Thread readThread;
    public int port = 9900; // represents the communication port with the phone
    public const int numOfMagics = 8;
    public List<double>[, ,] magics = new List<double>[numOfMagics, 5, 2];

    void Start()
    {
        string file = System.IO.File.ReadAllText(@"C:\Users\win7\Desktop\small.csv");
        file = file.Replace('\n', '\r');
        string[] lines = file.Split(new char[] { '\r' }, StringSplitOptions.RemoveEmptyEntries);
        InsertDataIntoList(lines);
        readThread = new Thread(new ThreadStart(ReceiveData));
        readThread.IsBackground = true;
        readThread.Start();
    }
    /// <summary>
    /// This function is sent to all game objects before the application is quit.
    /// </summary>
    void OnApplicationQuit()
    {
        //terminates the thread used for ReceiveData function
        readThread.Abort();
        // closes the udp client if its not null
        if (client != null)
            client.Close();
    }

    /// <summary>
    /// InsertDataIntoList function inserts the acceleration data received from the csv file into lists according to where each line belongs
    /// The first value of each line indicates to where it belongs (explained later)
    /// </summary>
    /// <param name="lines">Array of seperated lines extracted from the csv file.
    /// Each line represents a sample in specific axis (x or y accelerations)</param>
    private void InsertDataIntoList(string[] lines)
    {
        int numOfLines = lines.Length; //number of lines in the csv file
        
        //intializes the lists for the magics acceleration data
        //each list is actually representing a sample of a specific magic
        for (int k = 0; k < numOfMagics; k++)
            for (int i = 0; i < magics.GetLength(1); i++)
                for (int j = 0; j < magics.GetLength(2); j++)
                    magics[k, i, j] = new List<double>();
        //runs on all the rows
        for (int i = 0; i < numOfLines; i++)
        {
            string[] line = lines[i].Split(',');
            /*
             *The first value of each each row is written in the form of 1XYZ (for example 1000)
             *The unit digit (Z) represent whether its the x-axis accelerations or y-axis (1 or 0 respectively)
             *The tens digit (Y) represent the sample number of the the specific magic (from 0 - 4)
             *The hundres digit (X) represent the magic number (from 0 - 7)
             * For eg: 1231 means the current line belongs to the y-axis acclerations of the fourth sample of the third magic.
             */
            for (int j = 1; j < line.Length; j++)
                if (line[j] != "")
                    //reduces 48 as part of char to int conversion (48 represents 0 in ASCII. Therefore reducing 48 from the ASCII value will represent the numeric value (0-9)
                    magics[(int)line[0][1] - 48, (int)line[0][2] - 48, (int)line[0][3] - 48].Add(double.Parse(line[j]));
        }
    }
    private void ReceiveData()
    {
        List<double> xValues = new List<double>();
        List<double> yValues = new List<double>();
        double x = 0, y = 0, z = 0;
        int count = 0;
        bool done = false, started = false;
        client = new UdpClient(port);
        IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, port);
        try
        {
            while (!done)
            {
                byte[] bytes = client.Receive(ref groupEP);
                string[] sensors = Encoding.ASCII.GetString(bytes, 0, bytes.Length).Split(',');
                int LinearIndex = 0;
                //find the index of the linear acceleration
                for (int i = 0; i < sensors.Length; i++)
                    if (sensors[i].Equals(" 82")) LinearIndex = i;                    //save the values of the accelerations in the 3 axis
                x = Convert.ToDouble(sensors[LinearIndex + 1]);
                y = Convert.ToDouble(sensors[LinearIndex + 2]);
                z = Convert.ToDouble(sensors[LinearIndex + 3]);

                count++;
                if (z > 7 && started && count > 10)
                {
                    print("finish");
                    started = false;
                    count = 0;
                    IsGesture(xValues, yValues);
                    xValues.Clear();
                    yValues.Clear();
                }
                else if (z < -7 && !started && count > 10)
                {
                    print("start");

                    xValues.Clear();
                    yValues.Clear();
                    started = true;
                    count = 0;
                }
                else if (started)
                {
                    xValues.Add(x);
                    yValues.Add(y);
                }
            }
        }
        catch (Exception e)
        {
            print(e.ToString());
        }
        finally
        {
            client.Close();
        }
    }
    public double DTW(List<double> arr1, List<double> arr2)
    {
        int M = arr1.Count;
        int N = arr2.Count;
        var DTW = new double[M + 1, N + 1];
        DTW[0, 0] = 0;
        for (int j = 1; j <= M; j++)
            DTW[j, 0] = double.PositiveInfinity;
        for (int i = 1; i <= N; i++)
            DTW[0, i] = double.PositiveInfinity;
        for (int i = 1; i <= M; i++)
            for (int j = 1; j <= N; j++)
            {
                double cost = Math.Abs(arr1[i - 1] - arr2[j - 1]);
                DTW[i, j] = cost + Math.Min(DTW[i - 1, j],
                              Math.Min(DTW[i, j - 1],
                                   DTW[i - 1, j - 1]));
            }
        return DTW[M, N];
    }
    /// <summary>
    /// IsGesture function checks if the received pattern matches any magic
    /// This by calling the functions: CalculatePAD & GetRecognizedIndex
    /// </summary>
    /// <param name="xValues">A list of the x-axis acceleration of the received pattern</param>
    /// <param name="yValues">A list of the t-axis acceleration of the received pattern</param>
    public void IsGesture(List<double> xValues, List<double> yValues)
    {
        //average array will serve the purpose of storing avg distance between pattern and a specific magic 
        //its 2-dimensional because it calculates the x-axis and y-axis separately
        double[,] average = new double[numOfMagics, 2];
        //calculates the average distance of the received pattern with each magic
        CalculatePAD(average, xValues, yValues, 5);
        // gets the index of the recognized magic (-1 stands for didn't match)
        int foundIndex = GetRecognizedIndex(average);
        print(foundIndex == -1 ? "No such pattern" : foundIndex.ToString());
    }
    private int GetRecognizedIndex(double[,] average)
    {
        int index = -1;
        double[,] calcData = new double[,] { { 41.62, 36.11 }, { 36.43, 35.54 }, { 39.81, 58.51 }, { 43.96, 30.13 }, { 48.03, 41.64 }, { 24.84, 37.04 }, { 43.23, 37.6 }, { 35.73, 38.34 } };
        double epsilon = 2, bestX = 0;
        for (int k = 0; k < numOfMagics; k++)
            //checks if pattern has been recognized
            if (((average[k, 0] <= calcData[k, 0] * epsilon) && (average[k, 1] <= calcData[k, 1] * epsilon)))
                if ((calcData[k, 0] * epsilon - average[k, 0]) * (calcData[k, 1] * epsilon - average[k, 1]) > bestX)
                {
                    bestX = (calcData[k, 0] * epsilon - average[k, 0]) * (calcData[k, 1] * epsilon - average[k, 1]);
                    index = k;
                }
        return index;
    }
    /// <summary>
    /// CalculatePAD (Pattern-Average-Distance) function calculates the average distance with the received pattern.
    /// For each magic: It sums the DTW's distance of the received pattern with each magic's sample
    /// Then divided the sum by the number of samples to calculate the avg
    /// As the average distance is close to zero, the received pattern and the specific magic can fit.
    /// </summary>
    /// <param name="average">A fresh double array that will contain the average distance of the received pattern with any magic</param>
    /// <param name="xValues">A list of the x-axis acceleration of the received pattern</param>
    /// <param name="yValues">A list of the t-axis acceleration of the received pattern</param>
    /// <param name="numOfSamples">The number of samples of each magic. The received pattern will be compared to each sample separately</param>
    private void CalculatePAD(double[,] average, List<double> xValues, List<double> yValues, int numOfSamples)
    {
        //for each one of the 8 magics
        //calculates the average distance with the receive pattern (xValues, yValues)
        for (int k = 0; k < numOfMagics; k++)
        {
            for (int i = 0; i < numOfSamples; i++)
            {
                // sums the DTW's result of x & y axis
                average[k, 0] += DTW(xValues, magics[k, i, 0]);
                average[k, 1] += DTW(yValues, magics[k, i, 1]);
            }
            //calculates the avg of x & y axis
            average[k, 0] /= numOfSamples;
            average[k, 1] /= numOfSamples;
        }
    }
}
