using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Apu
    {

        private const int cpuFreq = 1789773;
        private int apuCycles;

        private bool iFlag = false;

        private bool mode0 = true;
        private bool disableInterrupt = true;

        public bool IFlag { get { bool value = iFlag; iFlag = false; return value; } }

        public Apu()
        {

        }

        public void Tick()
        {
            FrameSequencer();
            apuCycles++;
        }

        private void FrameSequencer()
        {
            if(mode0)
            {
                if(apuCycles % 3728 == 0)
                {
                    //Envelopes & triangle's linear counter (Quarter frame)	240hz
                }
                if(apuCycles % 7456 == 0)
                {
                    //Length counters &sweep units(Half frame) 120hz
                }
                if(apuCycles == 14914 && !disableInterrupt)
                {
                    iFlag = true;
                }
                if (apuCycles > 14914) apuCycles = 0;
            }
            else
            {
                if(apuCycles == 3728 || apuCycles == 7465 || apuCycles == 11185 || apuCycles == 18640)
                {
                    //Envelopes & triangle's linear counter (Quarter frame)	192 Hz (approx.), uneven timing
                }
                if (apuCycles == 7465 || apuCycles == 18640)
                {
                    //Length counters &sweep units(Half frame) 	96 Hz (approx.), uneven timing
                }
                if (apuCycles > 18640) apuCycles = 0;
            }
            
        }

        public void WriteApuRegister(int address, byte data)
        {
            address &= 0xFF;
            switch(address)
            {
                case 0x17:mode0 = ((data >> 7) & 1) != 0 ? false : true;
                        disableInterrupt = ((data >> 6) & 1) != 0 ? true : false;
                        apuCycles = 0;
                        break;
            }
        }

        public byte ReadApuRegister(int address)
        {
            address &= 0xFF;
            switch (address)
            {
                case 0x15:
                    int value = iFlag == true ? 1 << 6 : 0;
                    return (byte)value;
            }
            return 0;
        }
    }
        
}
