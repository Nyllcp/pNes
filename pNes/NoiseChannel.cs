using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class NoiseChannel
    {
        private const int cpuFreq = 1789773;
        private int frequency;

        private byte[] regs = new byte[4];
        //$400C	--lc.vvvv	Length counter halt, constant volume/envelope flag, and volume/envelope divider period (write)
        private bool lenghtCounterHalt = false;
        private bool constantVolume = false;
        private int volume;

        //$400E	M---.PPPP Mode and period(write)
        bool mode1 = false;
        byte period;

        //$400F	llll.l---	Length counter load and envelope restart(write)
        private int lenghtLoadCounter;

        

        private bool channelEnabled = false;
        private bool envelopeStart = false;


        private int shiftRegister = 0x7FF;
        
        private int timerCounter;
        private int envelopeCounter;
        private int envelopeVolume;

        int currentVolume;

        private byte[] lenghtCounterLookup = new byte[]
        {
            10,254, 20,  2, 40,  4, 80,  6, 160,  8, 60, 10, 14, 12, 26, 14,
            12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
        };
        private int[] timerPeriod = new int[]
        {
            4, 8, 14, 30, 60, 88, 118, 148, 188, 236, 354, 472, 708,  944, 1890, 3778
        };

        public bool LenghtCounterNotZero()
        {
            return lenghtLoadCounter != 0;
        }

        public int Sample;

        public NoiseChannel() { }

        public void Tick()
        {
            if (timerCounter-- <= 0)
            {
                timerCounter = timerPeriod[period];
                currentVolume = constantVolume ? volume : envelopeVolume;
                int shiftBit = mode1 ? (shiftRegister & 1) ^ ((shiftRegister >> 5) & 1) : (shiftRegister & 1) ^ ((shiftRegister >> 1) & 1);
                shiftRegister >>= 1;
                shiftRegister |= shiftBit << 14;
                if(lenghtLoadCounter != 0 && channelEnabled)
                {
                    Sample = (shiftRegister & 1) != 0 ? currentVolume : 0;
                }
                else Sample = 0;
            }
        }

        public void EnableChannel(bool enable)
        {
            channelEnabled = enable;
            if (!channelEnabled) lenghtLoadCounter = 0;
        }

        public void EnvelopeCounter()
        {
            if (envelopeStart)
            {
                envelopeStart = false;
                envelopeCounter = volume;
                envelopeVolume = 0xF;
            }
            else if (envelopeCounter > 0)
            {
                if (--envelopeCounter == 0)
                {
                    envelopeCounter = volume;
                    if (envelopeVolume > 0)
                    {
                        envelopeVolume--;
                    }
                    if (lenghtCounterHalt && envelopeVolume == 0)
                    {
                        envelopeVolume = 0xF;
                    }
                }
            }
        }

        public void LenghtCounter()
        {
            if (lenghtLoadCounter > 0 && !lenghtCounterHalt)
            {
                lenghtLoadCounter--;
            }
        }

        public void WriteReg(int address, byte data)
        {
            switch (address & 3)
            {
                case 0:
                    regs[0] = data;
                    lenghtCounterHalt = ((data >> 5) & 1) != 0 ? true : false;
                    constantVolume = ((data >> 4) & 1) != 0 ? true : false;
                    volume = data & 0xF;
                    break;
                case 1:
                    break;
                case 2:
                    mode1 = ((data >> 7) & 1) != 0;
                    period = (byte)(data & 0xF);
                    break;
                case 3:
                    regs[3] = data;
                    lenghtLoadCounter = lenghtCounterLookup[(data >> 3)];
                    envelopeStart = true;
                    break;
            }
        }
    }
}
