//CSK 10/23/2019 Redid code for Rover Pneumatic product line
//Logitech F710 (https://www.andymark.com/products/f710-wireless-logitech-game-controller)
//XD switch set to D
//Hold Logitech button until Mode light turns on - if robot on and usb dongle connected the mode light should be solid, if not connected light will flash
//Press mode button to switch to put in flight mode - 
//Controls left joystick forward and reverse, right joystick is steering

//CSK 10/25/2019
//Uncomment if using the CTRE display module
#define HASDISPLAY
//Uncomment to choose if using all talons or all victors
//#define TALONSRX
#define VICTORSPX

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System;
using System.Text;
using System.Threading;

using CTRE.Gadgeteer.Module;
using CTRE.Phoenix;
using CTRE.Phoenix.Controller;
using CTRE.Phoenix.MotorControl;
using CTRE.Phoenix.MotorControl.CAN;

namespace RoverPneumaticDrive
{
    //CSK 10/11.2016 Creating extension methods in C# https://msdn.microsoft.com/en-us/library/bb383977.aspx
    public static class Extensions
    {
        public static double ClampRange(this double value, double min, double max)
        {
            if (value >= min && value <= max)
                return value;
            else if (value > max)
            {
                return max;
            }
            else if (value < min)
            {
                return min;
            }
            else return 1;
        }
        public static double Deadband(this double value)
        {
            if (value < -0.10)
            {
                /* outside of deadband */
            }
            else if (value > +0.10)
            {
                /* outside of deadband */
            }
            else
            {
                /* within 10% so zero it */
                value = 0;
            }
            return value;
        }

    }

    public class Program
    {
        const double FULL_FORWARD = 1.0;
        const double FULL_REVERSE = -1.0;

#if TALONSRX
        /* Set up drive motors */
        static TalonSRX leftdrive1 = new TalonSRX(1);
        static TalonSRX leftdrive2 = new TalonSRX(2);
        static TalonSRX rightdrive1 = new TalonSRX(3);
        static TalonSRX rightdrive2 = new TalonSRX(4);
#elif VICTORSPX
        static VictorSPX leftdrive1 = new VictorSPX(1);
        static VictorSPX leftdrive2 = new VictorSPX(2);
        static VictorSPX rightdrive1 = new VictorSPX(3);
        static VictorSPX rightdrive2 = new VictorSPX(4);
#endif

        static GameController _gamepad = new GameController(UsbHostDevice.GetInstance());

        //CSK 4/7/2017 Kinda like #defines in C 
        //CSK 10/31/2018 Blue button is now 1 instead of 0
        const int BTNBLUEX = 1, BTNGREENA = 2, BTNREDB = 3, BTNYELLOWY = 4, BTNLEFT = 5, BTNRIGHT = 6, TRGRLEFT = 7, TRGRRIGHT = 8;
        const uint DEAD_MAN_BUTTON = 5;  //LB on gamepad
        //CSK 11/30/2018
        const uint LEFT_JOY_X = 0, LEFT_JOY_Y = 1, RIGHT_JOY_X = 2, ANALOGLEFT = 3, ANALOGRIGHT = 4, RIGHT_JOY_Y = 5;
   
#if (HASDISPLAY)
        //CSK 11/9/2018 Display works on Port1 and Port8
        static DisplayModule _displayModule = new DisplayModule(CTRE.HERO.IO.Port1, DisplayModule.OrientationType.Landscape);

        /* lets pick a font */
        static Font _smallFont = Properties.Resources.GetFont(Properties.Resources.FontResources.small);
        static Font _bigFont = Properties.Resources.GetFont(Properties.Resources.FontResources.NinaB);

        static DisplayModule.ResourceImageSprite _leftCrossHair, _rightCrossHair, _AMLogo; //, _LightBulb;
        static DisplayModule.LabelSprite _labelTitle, _labelBtn, _labelThrottle, _labelSteering;
        static int lftCrossHairOrigin = 35, rtCrossHairOrigin = 105;
#endif

        public static void Main()
        {
            //CSK 10/22/2019 .Follow method tells the follower motor to do whatever the lead motor does
            //The
            //Simplifies code a little bit
            leftdrive2.Follow(leftdrive1, FollowerType.PercentOutput);
            rightdrive2.Follow(rightdrive1, FollowerType.PercentOutput);
            //brushlessTest.Follow(rightdrive1, FollowerType.PercentOutput);

#if (HASDISPLAY)
            _AMLogo = _displayModule.AddResourceImageSprite(Properties.Resources.ResourceManager,
                                                                    Properties.Resources.BinaryResources.andymark_logo_160x26,
                                                                    Bitmap.BitmapImageType.Jpeg,
                                                                    0, 0);

            _labelTitle = _displayModule.AddLabelSprite(_bigFont, DisplayModule.Color.White, 0, 28, 160, 15);

            _labelBtn = _displayModule.AddLabelSprite(_smallFont, DisplayModule.Color.Cyan, 30, 60, 100, 10);

            _leftCrossHair = _displayModule.AddResourceImageSprite(Properties.Resources.ResourceManager,
                                                                   Properties.Resources.BinaryResources.crosshair,
                                                                   Bitmap.BitmapImageType.Jpeg,
                                                                   lftCrossHairOrigin, 100);

            _rightCrossHair = _displayModule.AddResourceImageSprite(Properties.Resources.ResourceManager,
                                                                    Properties.Resources.BinaryResources.crosshair,
                                                                    Bitmap.BitmapImageType.Jpeg,
                                                                    rtCrossHairOrigin, 100);

            _labelThrottle = _displayModule.AddLabelSprite(_bigFont, DisplayModule.Color.White, 0, 45, 75, 15);
            _labelSteering = _displayModule.AddLabelSprite(_bigFont, DisplayModule.Color.White, 80, 45, 75, 15);
#endif

            /* loop forever */
            while (true)
            {
                if (_gamepad.GetConnectionStatus() == CTRE.Phoenix.UsbDeviceConnection.Connected)
                {
                    /* feed watchdog to keep Talon's enabled */
                    CTRE.Phoenix.Watchdog.Feed();
                }
                /* drive robot using gamepad */
                Drive();
            }
        }

        static void Drive()
        {
            //Joystick values range from -1 to 1
            //CSK 11/30/2018 axis(0) is left joystick X axis - left right 
            //The minus signs are affected by the motor wiring polarity
            //If rotation is backwards from what is expected then either reverse the wires to the motor or change the minus sign
            double main_throttle = _gamepad.GetAxis(LEFT_JOY_Y);
            double steering = _gamepad.GetAxis(RIGHT_JOY_X);
            //twist is right joystick horizontal (steering)
            double leftMotors;
            double rightMotors;

            //CSK 12/6/2018 Make sure drive controllers are in coast mode
            leftdrive1.SetNeutralMode(NeutralMode.Coast);
            rightdrive1.SetNeutralMode(NeutralMode.Coast);

            leftMotors = -main_throttle.Deadband() - steering;
            rightMotors = main_throttle.Deadband() - steering;
            leftMotors = leftMotors.ClampRange(FULL_REVERSE, FULL_FORWARD);
            rightMotors = rightMotors.ClampRange(FULL_REVERSE, FULL_FORWARD);

            leftdrive1.Set(ControlMode.PercentOutput, leftMotors);
            rightdrive1.Set(ControlMode.PercentOutput, rightMotors);

#if (HASDISPLAY)
            //VVV CSK 11/2/2018 From HeroBridge_with_Arcade_And_Display code
            DisplayData(leftMotors, rightMotors);
#endif
            return;
        }
#if (HASDISPLAY)
        //CSK 11/2/2018 From HeroBridge_with_Arcade_And_Display code 
        static void DisplayData(double leftMotors, double rightMotors)
        {
            _labelThrottle.SetText( "L: " + leftMotors );
            _labelSteering.SetText( "R: " + rightMotors );

            int buttonPressed = GetFirstButton(_gamepad);
            if (buttonPressed < 0)
            {
                _labelBtn.SetColor((DisplayModule.Color)0xA0A0A0); // gray RGB
                _labelBtn.SetText("        No Buttons");
            }
            else
            {
                switch (buttonPressed)
                {
                    case BTNBLUEX: _labelBtn.SetColor(DisplayModule.Color.Blue); break;
                    case BTNGREENA: _labelBtn.SetColor(DisplayModule.Color.Green); break;
                    case BTNREDB: _labelBtn.SetColor(DisplayModule.Color.Red); break;
                    case BTNYELLOWY: _labelBtn.SetColor(DisplayModule.Color.Yellow); break;
                }
                _labelBtn.SetText("Pressed Button " + buttonPressed);
            }

            //CSK 01/11/2016 Display data on CTRE screen
            _leftCrossHair.SetPosition((int)(lftCrossHairOrigin + 15 * _gamepad.GetAxis(LEFT_JOY_X)), 100 + (int)(15 * _gamepad.GetAxis(LEFT_JOY_Y)));
            _rightCrossHair.SetPosition((int)(rtCrossHairOrigin + 15 * _gamepad.GetAxis(RIGHT_JOY_X)), 100 + (int)(15 * _gamepad.GetAxis(RIGHT_JOY_Y)));
            return;
        }
#endif

#if HASDISPLAY
        static int GetFirstButton(GameController gamepad)
        {
            for (uint i = 1; i < 16; ++i)
            {
                if (gamepad.GetButton(i))
                    return (int)i;
            }
            return -1;
        }

    }
#endif
}