using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace DynArTa
{
    public static class MessageHeader
    {
        //Message command bytes (0x80(128) to 0xFF(255)) - straight from Firmata.h
        public const byte DIGITAL_MESSAGE = 0x90;         //send data for a digital pin
        public const byte ANALOG_MESSAGE = 0xE0;          //send data for an analog pin (or PWM)
        public const byte DIGITAL_PULSE = 0x91;           //SysEx command to send a digital pulse

        public const byte PULSE_MESSAGE = 0xA0;           //proposed pulseIn/Out msg (SysEx)
        public const byte SHIFTOUT_MESSAGE = 0xB0;        //proposed shiftOut msg (SysEx)
        public const byte REPORT_ANALOG = 0xC0;           //enable analog input by pin #
        public const byte REPORT_DIGITAL = 0xD0;          //enable digital input by port pair
        public const byte START_SYSEX = 0xF0;             //start a MIDI SysEx msg
        public const byte SET_PIN_MODE = 0xF4;            //set a pin to INPUT/OUTPUT/PWM/etc
        public const byte END_SYSEX = 0xF7;               //end a MIDI SysEx msg
        public const byte REPORT_VERSION = 0xF9;          //report firmware version
        public const byte SYSTEM_RESET = 0xFF;            //reset from MIDI
        public const byte QUERY_FIRMWARE = 0x79;          //query the firmware name

        //extended command set using sysex (0-127/0x00-0x7F)
        //0x00-0x0F reserved for user-defined commands */

        public const byte EXTENDED_ANALOG = 0x6F;         //analog write (PWM, Servo, etc) to any pin
        public const byte PIN_STATE_QUERY = 0x6D;         //ask for a pin's current mode and value
        public const byte PIN_STATE_RESPONSE = 0x6E;      //reply with pin's current mode and value
        public const byte CAPABILITY_QUERY = 0x6B;        //ask for supported modes and resolution of all pins
        public const byte CAPABILITY_RESPONSE = 0x6C;     //reply with supported modes and resolution
        public const byte ANALOG_MAPPING_QUERY = 0x69;    //ask for mapping of analog to pin numbers
        public const byte ANALOG_MAPPING_RESPONSE = 0x6A; //reply with mapping info

        public const byte SERVO_CONFIG = 0x70;            //set max angle, minPulse, maxPulse, freq
        public const byte STRING_DATA = 0x71;             //a string message with 14-bits per char
        public const byte SHIFT_DATA = 0x75;              //a bitstream to/from a shift register
        public const byte I2C_REQUEST = 0x76;             //send an I2C read/write request
        public const byte I2C_REPLY = 0x77;               //a reply to an I2C read request
        public const byte I2C_CONFIG = 0x78;              //config I2C settings such as delay times and power pins
        public const byte REPORT_FIRMWARE = 0x79;         //report name and version of the firmware
        public const byte SAMPLING_INTERVAL = 0x7A;       //set the poll rate of the main loop
        public const byte SYSEX_NON_REALTIME = 0x7E;      //MIDI Reserved for non-realtime messages
        public const byte SYSEX_REALTIME = 0x7F;          //MIDI Reserved for realtime messages
    }

    public class Board
    {
        //The Base class for any board.
        firmata_version = None
        firmware = None
        firmware_version = None
        _command_handlers = {}
        _command = None
        _stored_data = []
        _parsing_sysex = false;
        public SerialPort Sp { get; private set; }
        public Pin[] AnalogPins { get; private set; }
        public Pin[] DigitalPins { get; private set; }
        private object _layout;
        public string Name { get; private set; }

        private const int BoardSetupWaitTime = 5;

        public Board(string serialPort, object layout = null, int baudRate = 57600, string name = "")
        {
            Sp = new SerialPort(serialPort, baudRate);
            //Allow 5 secs for Arduino's auto-reset to happen
            //Alas, Firmata blinks its version before printing it to serial
            //For 2.3, even 5 seconds might not be enough.
            //TODO Find a more reliable way to wait until the board is ready
            PassTime(BoardSetupWaitTime);
            Name = name;
            _layout = layout;
            if (string.IsNullOrEmpty(Name))
                Name = serialPort;

            if (layout != null)
                SetupLayout(layout);
            else
                AutoSetup();

            //Iterate over the first messages to get firmware data
            while (BytesAvailable())
                Iterate();
            //TODO Test whether we got a firmware name and version, otherwise there
            //probably isn't any Firmata installed
        }

        ~Board()
        {
            //The connection with the a board can get messed up when a script is
            //closed without calling board.exit() (which closes the serial
            //connection). Therefore also do it here and hope it helps.

            Exit();
        }

        public void Exit()
        {
            //Call this to exit cleanly.
            //First detach all servo's, otherwise it somehow doesn't want to close...
            if (DigitalPins != null)
            {
                foreach (Pin p in DigitalPins)
                    if (p.Mode == PinMode.Servo)
                        p.Mode = PinMode.Output;
            }

            if (Sp != null)
                Sp.Close();
        }


        public override string ToString()
        {
            return string.Format("Board{0} on {1}", Name, Sp.PortName);
        }

        public void SendAsTwoBytes(int val)
        {
            byte[] msg = {(byte)(val & 0xFF), (byte)(val >> 7)};
            Sp.Write(msg, 0, msg.Length);
        }
        

        private void SetupLayout(object board_layout)
        {
            //Setup the Pin instances based on the given board layout.
            //Create pin instances based on board layout
            AnalogPins = new Pin[board_layout.analog.Length];
            foreach (Pin p in board_layout.analog)
            {

                AnalogPins.CopyTo()
            }
            for i in board_layout['analog']:

                AnalogPins.append(Pin(self, i))

            DigitalPins = new Pin[board_layout.digital.Length];
            this.digital_ports = []
            for i in range(0, len(board_layout['digital']), 8):
                num_pins = len(board_layout['digital'][i:i + 8])
                port_number = int(i / 8)
                this.digital_ports.append(Port(self, port_number, num_pins))

            //Allow to access the Pin instances directly
            for port in this.digital_ports:
                this.digital += port.pins

            //Setup PWM pins
            for i in board_layout['pwm']:
                this.digital[i].PWM_CAPABLE = true

            //Disable certain ports like Rx/Tx and crystal ports
            for i in board_layout['disabled']:
                this.digital[i].mode = UNAVAILABLE

            //Create a dictionary of 'taken' pins. Used by the get_pin method
            this.taken = {'analog': dict(map(lambda p: (p.pin_number, false), this.analog)),
                          'digital': dict(map(lambda p: (p.pin_number, false), this.digital))}

            SetDefaultHandlers();
        }

        private void SetDefaultHandlers()
        {
            //Setup default handlers for standard incoming commands
            add_cmd_handler(ANALOG_MESSAGE, this._handle_analog_message);
            add_cmd_handler(DIGITAL_MESSAGE, this._handle_digital_message);
            add_cmd_handler(REPORT_VERSION, this._handle_report_version);
            add_cmd_handler(REPORT_FIRMWARE, this._handle_report_firmware);
        }


        private void AutoSetup()
        {
            //Automatic setup based on Firmata's "Capability Query"
            this.add_cmd_handler(CAPABILITY_RESPONSE, this._handle_report_capability_response)
            this.send_sysex(CAPABILITY_QUERY, [])
            this.pass_time(0.1) //Serial SYNC

            while this.bytes_available():
                this.iterate()

            //handle_report_capability_response will write this._layout
            if this._layout:
                this.setup_layout(this._layout)
            else:
                raise IOError("Board detection failed.")
        }



        private void add_cmd_handler(cmd , func )
        {
            //Adds a command handler for a command.
            len_args = len(inspect.getargspec(func)[0])

            def add_meta(f):
                def decorator(*args, **kwargs):
                    f(*args, **kwargs)
                decorator.bytes_needed = len_args - 1  //exclude self
                decorator.__name__ = f.__name__
                return decorator
            func = add_meta(func)
            this._command_handlers[cmd] = func
        }

        public Pin GetPin(object pin_def)
        {
            //Returns the activated pin given by the pin definition.
            //May raise an ``InvalidPinDefError`` or a ``PinAlreadyTakenError``.
            //:arg pin_def: Pin definition as described below,
            //    but without the arduino name. So for example ``a:1:i``.
            //'a' analog pin     Pin number   'i' for input
            //'d' digital pin    Pin number   'o' for output
            //                                'p' for pwm (Pulse-width modulation)
            //All seperated by ``:``.

            if type(pin_def) == list:
                bits = pin_def
            else:
                bits = pin_def.split(':')
            a_d = bits[0] == 'a' and 'analog' or 'digital'
            part = getattr(self, a_d)
            pin_nr = int(bits[1])
            if pin_nr >= len(part):
                raise InvalidPinDefError('Invalid pin definition: {0} at position 3 on {1}'.format(pin_def, this.name))
            if getattr(part[pin_nr], 'mode', None) == UNAVAILABLE:
                raise InvalidPinDefError('Invalid pin definition: UNAVAILABLE pin {0} at position on {1}'.format(pin_def, this.name))
            if this.taken[a_d][pin_nr]:
                raise PinAlreadyTakenError('{0} pin {1} is already taken on {2}'.format(a_d, bits[1], this.name))
            //ok, should be available
            pin = part[pin_nr]
            this.taken[a_d][pin_nr] = true
            if pin.type is DIGITAL:
                if bits[2] == 'p':
                    pin.mode = PWM
                elif bits[2] == 's':
                    pin.mode = SERVO
                elif bits[2] != 'o':
                    pin.mode = INPUT
            else:
                pin.enable_reporting()
            return pin
        }


        private void PassTime(int t)
        {
            //Non-blocking time-out for ``t`` seconds.
            cont = time.time() + t
            while
                time.time() < cont:
                time.sleep(0);
        }

        private void SendSysex(byte sysexCmd, byte[] data)
        {
            //Sends a SysEx msg.
            //:arg sysex_cmd: A sysex command byte
            //: arg data: a bytearray of 7-bit bytes of arbitrary data
            List<byte> msg = new List<byte> {MessageHeader.START_SYSEX, sysexCmd};
            msg.AddRange(data);
            msg.Add(MessageHeader.END_SYSEX);
            Sp.Write(msg.ToArray(), 0, msg.Count);
        }



        private bool BytesAvailable()
        {
            return Sp.BytesToRead > 0;
        }
        

    def iterate(self):
        """
        Reads and handles data from the microcontroller over the serial port.
        This method should be called in a main loop or in an :class:`Iterator`
        instance to keep this boards pin values up to date.
        """
        byte = this.sp.read()
        if not byte:
            return
        data = ord(byte)
        received_data = []
        handler = None
        if data < START_SYSEX:
            //These commands can have 'channel data' like a pin nummber appended.
            try:
                handler = this._command_handlers[data & 0xF0]
            except KeyError:
                return
            received_data.append(data & 0x0F)
            while len(received_data) < handler.bytes_needed:
                received_data.append(ord(this.sp.read()))
        elif data == START_SYSEX:
            data = ord(this.sp.read())
            handler = this._command_handlers.get(data)
            if not handler:
                return
            data = ord(this.sp.read())
            while data != END_SYSEX:
                received_data.append(data)
                data = ord(this.sp.read())
        else:
            try:
                handler = this._command_handlers[data]
            except KeyError:
                return
            while len(received_data) < handler.bytes_needed:
                received_data.append(ord(this.sp.read()))
        //Handle the data
        try:
            handler(*received_data)
        except ValueError:
            pass

        public Tuple get_firmata_version()
        {
            //Returns a version tuple (major, minor) for the firmata firmware on the
            //board.
            return this.firmata_version
        }

        public void ServoConfig(pin , min_pulse =544, max_pulse =2400, angle =0)
        {
            //Configure a pin as servo with min_pulse, max_pulse and first angle.
            //``min_pulse`` and ``max_pulse`` default to the arduino defaults.

            if pin > len(this.digital) or this.digital[pin].mode == UNAVAILABLE:
                raise IOError("Pin {0} is not a valid servo pin".format(pin))

            List<byte> data = new List<byte> {pin};
            data.AddRange(to_two_bytes(min_pulse));
            data.AddRange(to_two_bytes(max_pulse));
            SendSysex(MessageHeader.SERVO_CONFIG, data.ToArray());

            //set pin._mode to SERVO so that it sends analog messages
            //don't set pin.mode as that calls this method
            digital[pin]._mode = SERVO
            digital[pin].write(angle)
        }




    //Command handlers
    def _handle_analog_message(self, pin_nr, lsb, msb):
        value = round(float((msb << 7) + lsb) / 1023, 4)
        //Only set the value if we are actually reporting
        try:
            if this.analog[pin_nr].reporting:
                this.analog[pin_nr].value = value
        except IndexError:
            raise ValueError

    def _handle_digital_message(self, port_nr, lsb, msb):
        """
        Digital messages always go by the whole port. This means we have a
        bitmask which we update the port.
        """
        mask = (msb << 7) + lsb
        try:
            this.digital_ports[port_nr]._update(mask)
        except IndexError:
            raise ValueError

    def _handle_report_version(self, major, minor):
        this.firmata_version = (major, minor)

    def _handle_report_firmware(self, *data):
        major = data[0]
        minor = data[1]
        this.firmware_version = (major, minor)
        this.firmware = two_byte_iter_to_str(data[2:])

    def _handle_report_capability_response(self, *data):
        charbuffer = []
        pin_spec_list = []

        for c in data:
            if c == CAPABILITY_RESPONSE:
                continue

            charbuffer.append(c)
            if c == 0x7F:
                //A copy of charbuffer
                pin_spec_list.append(charbuffer[:])
                charbuffer = []

        this._layout = pin_list_to_board_dict(pin_spec_list)
    }
}
