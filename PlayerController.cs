﻿using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;


public class PlayerController : MonoBehaviour
{

    public Thread readThread; // A thread to call ReceiveData function in parallel to Update function
    public UdpClient client; //udpclient for receiving udp msgs with the phone's sensors data
    public int port = 9900; // the communication port with phone
    public float initialAcceleration = 0, initialRotation = 0; // the inital accelerations the phone started with (base mode)
    public float rotationAngle = 0, forwardSpeed = 0; //num of degress & movement speed to control the object with
    public CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        //initializes & starts the readThread - responsible for udp msgs
        readThread = new Thread(new ThreadStart(ReceiveData));
        readThread.IsBackground = true;
        readThread.Start();
    }

    // Update is called once per frame
    /// <summary>
    /// Update is called once per frame
    /// Rotates & Moves the object according to the calculated data from the sensors
    /// rotationAngle & forwardSpeed are global variables the are changing every call to MoveObj function
    /// </summary>
    void Update()
    {
        //rotates the object a number of degress equal to rotationAngle
        transform.Rotate(0, rotationAngle, 0);
        //forward represents the (1,0,0) vector according to the world space (Local is relative to object; World is relative to the game world)
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        //moves the object using the control in the direction of forward vector where multiplied by forwardSpeed
        controller.SimpleMove(forward * forwardSpeed);
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
    /// ReceiveData function receives a string via udp represents phone's sensors data
    /// It finds the appropiate acceleration data for forward movement and rotation
    /// calls MoveObj function to calculate and update the speed of movement and rotation's angle
    /// </summary>
    private void ReceiveData()
    {
        // creates a udpclient with the specific port to communicate with phone
        client = new UdpClient(port);
        while (true)
        {
            try
            {
                // receive bytes
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);

                string[] sensors = Encoding.ASCII.GetString(data, 0, data.Length).Split(','); // represent the whole msg received by udp
                int accelerationIndex = 0;
                //print(Encoding.ASCII.GetString(data, 0, data.Length));
                //finds the acceleration data in the udp msg
                // 3 is the index for accelerometer sensor
                for (int i = 0; i < sensors.Length; i++)
                {
                    if (sensors[i].Equals(" 3")) accelerationIndex = i;
                }
                //accelerationIndex + 1 / +2 is the x / y axis acceleration 
                float forwardAcceleration = float.Parse(sensors[accelerationIndex + 2]);
                float rotationAcceleration = float.Parse(sensors[accelerationIndex + 1]);
                MoveObj(forwardAcceleration, rotationAcceleration); //calls MoveObj to calculate and update the speed of movement and rotation's angle
            }
            catch (Exception err)
            {
                print(err.ToString());
            }
        }
    }
    /// <summary>
    /// MoveObj receives acceleration data for movement and camera rotation
    /// Calculates & Changes rotationAngle forwardSpeed used in Update function
    /// </summary>
    /// <param name="forwardAcceleration"> represents the forward/backwards movement acceleration</param>
    /// <param name="rotationAcceleration">represents the camera rotation acceleration</param>
    private void MoveObj(float forwardAcceleration, float rotationAcceleration)
    {
        //if its the first received acceleration, initializes the default acceleration to compare with later on
        if (initialAcceleration == 0)
            initialAcceleration = forwardAcceleration;

        else
        {
            // deltaAcceleration is the difference between the initial acceleration and the received one
            float deltaAcceleration = forwardAcceleration - initialAcceleration;
            // checks if delta is above 1, to avoid the effects of noise on sensor, so it wont auto move
            if (Math.Abs(deltaAcceleration) > 1)
            {
                // sets the moving speed according to delta
                // the purpose of minus is to switch the movement directions: when you tilt phone upside, object will move backwards
                forwardSpeed = -deltaAcceleration * 5f;
            }
            // if the delta is smaller than 1, dont move the character -> speed set to zero
            else forwardSpeed = 0;
        }
        // same idea as done with forward acceleration:
        // intializes and then calculates delta, if delta is greater than 1 changes the rotation angle, else sets to 0
        if (initialRotation == 0)
            initialRotation = rotationAcceleration;

        else
        {
            float deltaRotation = rotationAcceleration - initialRotation;
            if (Math.Abs(deltaRotation) > 1)
            {
                rotationAngle = -deltaRotation / 2;
            }
            else rotationAngle = 0;
        }
    }
}