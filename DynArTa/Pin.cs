using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynArTa
{
    public enum PinMode
    {
        Unavailable = -1,
        Input = 0,
        Output = 1,
        Analog = 2,
        Pwm = 3,
        Servo = 4
    }

    public enum PinType
    {
        Analog, Digital
    }


    public class Pin
    {
        private Board Board { get; set; }
        public PinType Type { get; private set; }
        public Port Port { get; private set; }
        public int PinNumber { get; private set; }
        public bool PwmCapable { get; private set; }
        public PinMode Mode { get; set; }
        public bool Reporting { get; set; }
        public int Value { get; set; }


        public Pin(Board board, int pinNumber, PinType type = PinType.Analog, Port port = null)
        {
            Board = board;
            PinNumber = pinNumber;
            Type = type;
            Port = port;
            PwmCapable = false;
            Mode = type == PinType.Digital ? PinMode.Output : PinMode.Input;
            Reporting = false;
            Value = 0;
        }


        public override string ToString()
        {
            return string.Format("{0} pin {1}", Type, PinNumber);
        }



        public void SetMode(PinMode mode)
        {
            if (mode == PinMode.Unavailable)
            {
                Mode = PinMode.Unavailable;
                return;
            }
            
            if (Mode == PinMode.Unavailable)
                throw new ArgumentException(string.Format("{0} can not be used through Firmata", this));

            if (mode == PinMode.Pwm && !PwmCapable)
                throw new ArgumentException(string.Format("{0} does not have PWM capabilities", this));

            if (mode == PinMode.Servo)
            {
                if (Type != PinType.Digital)
                    throw new ArgumentException(string.Format("Only digital pins can drive servos! {0} is not digital", this));
                Mode = PinMode.Servo;
                Board.ServoConfig(PinNumber);
                return;
            }

            //Set mode with SET_PIN_MODE message
            Mode = mode;
            byte[] msg = {MessageHeader.SET_PIN_MODE, (byte)PinNumber, (byte)Mode};
            Board.Sp.Write(msg, 0, msg.Length);
            if (mode == PinMode.Input)
                EnableReporting();
        }

        public void EnableReporting()
        {
            //Set an input pin to report values.
            if (Mode != PinMode.Input)
                throw new ArgumentException(string.Format("{0} is not an input and can therefore not report", this));

            if (Type == PinType.Analog)
            {
                Reporting = true;
                byte[] msg = {(byte)(MessageHeader.REPORT_ANALOG + PinNumber), 1};
                Board.Sp.Write(msg, 0, msg.Length);
            }
            else
            {
                //TODO This is not going to work for non-optimized boards like Mega
                Port.EnableReporting();
            }
        }


        public void DisableReporting()
        {
            //Disable the reporting of an input pin.
            if (Type == PinType.Analog)
            {
                Reporting = false;
                byte[] msg = {(byte)(MessageHeader.REPORT_ANALOG + PinNumber), 0};
                Board.Sp.Write(msg, 0, msg.Length);
            }
            else
            {
                Port.DisableReporting();
                //TODO This is not going to work for non-optimized boards like Mega
            }
        }



        public int Read()
        {
            if (Mode == PinMode.Unavailable)
                throw new ArgumentException(string.Format("Cannot read pin {0}", this));
            return Value;
        }


        //Output a voltage from the pin
        //:arg value: Uses value as a boolean if the pin is in output mode, or
        //    expects a float from 0 to 1 if the pin is in PWM mode. If the pin
        //    is in SERVO the value should be in degrees.
        public void Write(int value)
        {
            if (Mode == PinMode.Unavailable)
                throw new ArgumentException(string.Format("{0} can not be used through Firmata", this));
            if (Mode == PinMode.Input)
                throw new ArgumentException(
                    string.Format("{0} is set up as an INPUT and can therefore not be written to", this));
            if (Value != value)
            {
                Value = value;
                if (Mode == PinMode.Output)
                {
                    if (Port != null)
                        Port.Write();
                    else
                    {
                        byte[] msg = {MessageHeader.DIGITAL_MESSAGE, (byte)PinNumber, (byte)Value};
                        Board.Sp.Write(msg, 0, msg.Length);
                    }
                }
                
                else if (Mode == PinMode.Pwm)
                {
                    Value = value;
                    byte[] msg = {(byte)(MessageHeader.ANALOG_MESSAGE + PinNumber), (byte)(Value & 0xFF), (byte)(Value >> 7)};
                    Board.Sp.Write(msg, 0, msg.Length);
                }
                else if (Mode == PinMode.Servo)
                {
                    Value = value;
                    byte[] msg = {(byte)(MessageHeader.ANALOG_MESSAGE + PinNumber), (byte)(Value & 0xFF), (byte)(Value >> 7)};
                    Board.Sp.Write(msg, 0, msg.Length);
                }
            }
        }
    }
}
