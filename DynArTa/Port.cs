using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynArTa
{
    public class Port
    {
        private Board Board { get; set; }
        public byte PortNumber { get; private set; }
        public bool Reporting { get; private set; }
        public Pin[] Pins { get; private set; }

        //An 8-bit port on the board.
        public Port(Board board, byte portNumber, int pinCount = 8)
        {
            Board = board;
            PortNumber = portNumber;
            Reporting = false;

            Pins = new Pin[pinCount];
            for (int i = 0; i < pinCount; ++i)
            {
                Pins[i] = new Pin(Board, i + PortNumber*8, PinType.Digital, this);
            }
        }

        public override string ToString()
        {
            return string.Format("Digital Port {0} on {1}", PortNumber, Board);
        }

        public void EnableReporting()
        {
            //Enable reporting of values for the whole port.
            Reporting = true;
            byte[] msg = {(byte)(MessageHeader.REPORT_DIGITAL + PortNumber), 1};
            Board.Sp.Write(msg, 0, msg.Length);

            //TODO Shouldn't this happen at the pin?
            foreach (Pin p in Pins)
                if (p.Mode == PinMode.Input)
                    p.Reporting = true;
        }

        public void DisableReporting()
        {
            //Disable the reporting of the port.
            Reporting = false;
            byte[] msg = {(byte)(MessageHeader.REPORT_DIGITAL + PortNumber), 0};
            Board.Sp.Write(msg, 0, msg.Length);
        }


        //Set the output pins of the port to the correct state.
        public void Write()
        {
            int mask = 0;
            foreach (Pin p in Pins)
            {
                if (p.Mode == PinMode.Output && p.Value == 1)
                {
                    int pin_nr = p.PinNumber % 8;
                    mask |= 1 << pin_nr;
                }
            }

            byte[] msg = {(byte)(MessageHeader.DIGITAL_MESSAGE + PortNumber), (byte)(mask & 0xFF), (byte)(mask >> 7)};
            Board.Sp.Write(msg, 0, msg.Length);
        }


        //private void Update(int mask)
        //{
        //    //Update the values for the pins marked as input with the mask.
        //    if (Reporting == true)
        //        foreach (Pin p in Pins)
        //            if (p.Mode == PinMode.Input)
        //            {
        //                int pin_nr = p.PinNumber % 8;
        //                p.Value = (mask & (1 << pin_nr)) > 0;
        //            }
        //}
    }
}
