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
    public UdpClient client; //udpclient for receiving udp msgs with the phone's sensors data
    public Thread readThread; // A thread to call ReceiveData function
    public int port = 9900; // the communication port with phone
    public const int numOfMagics = 9;
    public List<double>[, ,] magics = new List<double>[numOfMagics, 5, 2]; //each element represnets a sample of a speicifc magic in a specific axis (x/y)

    void Start()
    {
        //loads the CSV file with the accelerations data
        string file = System.IO.File.ReadAllText(@"C:\Users\win7\Desktop\accelerations.csv");
        file = file.Replace('\n', '\r');
        //seperates the csv file into lines
        string[] lines = file.Split(new char[] { '\r' }, StringSplitOptions.RemoveEmptyEntries);
        //inserts csv file data into lists according to the magics
        InsertDataIntoList(lines);
        //initializes & starts the readThread - responsible for udp msgs
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
    /// <summary>
    /// ReceiveData function receives a string via udp represents phone's sensors data
    /// It finds the appropiate acceleration data for linear acceleration sensor (x,y,z axis)
    /// Recognizes the trigger to start & end pattern's data saving and then calls IsGesture to recognize the most matched magic
    /// </summary>
    private void ReceiveData()
    {
        List<double> xValues = new List<double>(); //will store the x-axis accelerations of the received pattern
        List<double> yValues = new List<double>(); //will store the y-axis accelerations of the received pattern
        double xAcceleration = 0, yAcceleration = 0, zAcceleration = 0; //represents the receive acceleration of each axis
        bool isStarted = false; // the state of pattern: if started - saves data, otherwise waits for a trigger
        client = new UdpClient(port); // creates a udpclient with the specific port to communicate with phone
        IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, port);
        try
        {
            while (true)
            {
                // receive bytes
                byte[] bytes = client.Receive(ref groupEP);
                string[] sensors = Encoding.ASCII.GetString(bytes, 0, bytes.Length).Split(',');
                int linearIndex = 0;
                //finds the index of the linear acceleration in the receive udp msg
                for (int i = 0; i < sensors.Length; i++)
                    if (sensors[i].Equals(" 82")) linearIndex = i;
                //save the values of the accelerations in the 3 axis. linearIndex +1/+2/+3 is the x/y/z axis acceleration 
                xAcceleration = Convert.ToDouble(sensors[linearIndex + 1]);
                yAcceleration = Convert.ToDouble(sensors[linearIndex + 2]);
                zAcceleration = Convert.ToDouble(sensors[linearIndex + 3]);

                
                if (zAcceleration > 7 && isStarted && xValues.Count > 10) //if already started (means saved data into lists) and the finish trigger is recognized & pattern's length > 10 = start recognizing the pattern
                {
                    print("finish");
                    isStarted = false;
                    IsGesture(xValues, yValues); //After done storing pattern's data - tries to recognize it
                    xValues.Clear(); //Clears the lists
                    yValues.Clear();
                }
                else if (zAcceleration < -7 && !isStarted)
                {
                    print("start");
                    xValues.Clear(); //Clears the lists (A new fresh pattern)
                    yValues.Clear();
                    isStarted = true; //Changes the bool variable to true, means started to save pattern's accelerations
                }
                else if (isStarted) // if started --> save data into lists
                {
                    xValues.Add(xAcceleration);
                    yValues.Add(yAcceleration);
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
    /// <summary>
    /// Dynamic Time Warping (DTW) is an algorithm for measuring similarity between two temporal sequences which may vary in time or speed
    /// </summary>
    /// <param name="list1">First sequence - a list of accelerations in a specific axis</param>
    /// <param name="list2">Second sequence - a list of accelerations in a specific axis</param>
    /// <returns>DTW calculates & returns an optimal match between two given sequences (A double value representing a distance)</returns>
    public double DTW(List<double> list1, List<double> list2)
    {
        /*
         *Algorithm: Construct a M X N distance matric. Each cell [i,j] represents the distance between the i-th element of list1 and the j-th element of list2. 
         *The best alignment between two lists can be seen as finding the shortest path to go from the first cell [0,0] to the bottom-right cell [M,N] of that matrix. 
         *The length of a path is simply the sum of all the cells that were visited along that path.
         *Explanation taken from : mblondel.org
         */
        int M = list1.Count;
        int N = list2.Count;
        double[,] DTW = new double[M + 1, N + 1]; //creates a matrix. its size is according to the received lists' length. 
        DTW[0, 0] = 0;
        //sets infinite value on the first row without the element in index [0,0]
        for (int i = 1; i <= M; i++)
            DTW[i, 0] = double.PositiveInfinity;
        //sets infinite value on the first column without the element in index [0,0]
        for (int j = 1; j <= N; j++)
            DTW[0, j] = double.PositiveInfinity;

        //calculates the shortest path
        for (int i = 1; i <= M; i++)
            for (int j = 1; j <= N; j++)
            {
                double cellDistance = Math.Abs(list1[i - 1] - list2[j - 1]);
                // The value in the current cell of the matrix will be the shortest distance (path) till reaching this cell + the cellDistance calculated above
                // Using Math.Min on the cell's neighbors that already been visited
                DTW[i, j] = cellDistance + Math.Min(DTW[i - 1, j], Math.Min(DTW[i, j - 1], DTW[i - 1, j - 1]));
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
    /// <summary>
    /// GetRecognizedIndex function finds the most matched magic (and the magic's index) to the received pattern. If no such magic - returns -1.
    /// </summary>
    /// <param name="average">A double array that contains the average distance of the received pattern with any magic in each axis</param>
    /// <returns>Returns index representing the recognized magic. -1 means no such magic</returns>
    private int GetRecognizedIndex(double[,] average)
    {
       
        int index = -1; //represnets the index of the recognized gesture. -1 means couldnt find.
        //calcData is an array that stores pre-made calculations of the average distance between each 5 samples of a specific magic
        //its 2 dimensional since the magic has x & y axis
        double[,] calcData = new double[,] { { 83.24, 72.22 }, { 72.86, 71.08 }, { 79.62, 117.02 }, { 87.92, 60.26 }, { 96.06, 83.28 }, { 49.68, 74.08 }, { 86.46, 75.2 }, { 71.46, 76.86 }, { 55.82, 52.8 } };

        double maxResult = 0; // This variable will be used in the part of finding the most matched magic (will save a maximum value related to (x-axis's average) * (y-axis's average)
        for (int k = 0; k < numOfMagics; k++)
            /*
             * checks if pattern has been recognized:
             * The x-axis & y-axis average distances must be smaller than the calculated average (The pre-calculated average between the 5 samples)
            */
            if (((average[k, 0] <= calcData[k, 0]) && (average[k, 1] <= calcData[k, 1])))
                
                //determines the most matched magic by accuracity - only for recognized magics
                //saves the index of the most matched magic/
                //The logic is: The more the average of each axis is closer to zero, the higher the precision is. Thats how the most accurate magic is determined
                if ((calcData[k, 0] - average[k, 0]) * (calcData[k, 1] - average[k, 1]) > maxResult)
                {
                    maxResult = (calcData[k, 0] - average[k, 0]) * (calcData[k, 1] - average[k, 1]);
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
