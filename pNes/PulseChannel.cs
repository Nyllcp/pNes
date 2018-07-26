using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class PulseChannel
    {
        private const int cpuFreq = 1789773;
        private int frequency;

        private byte[] regs = new byte[4];
        //$4000 / $4004 DDLC VVVV
        private int duty;
        private bool lenghtCounterHalt = false;
        private bool constantVolume = false;
        private int volume;

        //$4001 / $4005	EPPP NSSS
        private bool sweepEnabled = false;
        private int sweepPeriod;
        private bool negate = false;
        private int sweepShift;
        

        //$4002 / $4006	TTTT TTTT   Timer low(T)
        private int timer;

        

        //$4003 / $4007	LLLL LTTT	Length counter load (L), timer high (T)
        private int lenghtLoadCounter;

        private bool channelEnabled = false;
        private bool envelopeStart = false;
        private bool reloadSweep = false;
        private bool validSweep = true;

        private int timerCounter;
        private int sweepPeriodCounter;
        private int envelopeCounter;
        private int envelopeVolume;
        int currentVolume;
        private byte[] dutyCycles = new byte[]
        {
            0x40, 0x60, 0x78,0x9F
        };

        private byte[] lenghtCounterLookup = new byte[]
        {
            10,254, 20,  2, 40,  4, 80,  6, 160,  8, 60, 10, 14, 12, 26, 14,
            12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
        };
        private int dutyCounter;

        public int Sample;

        public PulseChannel() { }

        public void Tick()
        {
            if(timerCounter-- <= 0)
            {
                timerCounter = timer;
                currentVolume = constantVolume ? volume : envelopeVolume;
                dutyCounter &= 7;
                if (lenghtLoadCounter != 0 && channelEnabled && validSweep)
                {
                    Sample = (dutyCycles[duty] >> dutyCounter++) != 0 ? currentVolume : 0;
                }
                else Sample = 0;
                
            }
        }

        private  void CalculateValidSweep()
        {
            validSweep = (timer >= 0x8) && ((negate) || (((timer + (timer >> sweepShift)) & 0x800) == 0));
        }

        public bool LenghtCounterNotZero()
        {
            return lenghtLoadCounter != 0;
        }

        public void EnableChannel(bool enable)
        {
            channelEnabled = enable;
            if (!channelEnabled) lenghtLoadCounter = 0;
        }

        public void EnvelopeCounter()
        {
            if(envelopeStart)
            {
                envelopeStart = false;
                envelopeCounter = volume;
                envelopeVolume = 0xF;
            }
            else if(envelopeCounter > 0)
            {
                if(--envelopeCounter == 0)
                {
                    envelopeCounter = volume;
                    if(envelopeVolume > 0)
                    {
                        envelopeVolume--;
                    }
                    if(lenghtCounterHalt && envelopeVolume == 0)
                    {
                        envelopeVolume = 0xF;
                    }
                }
            }
        }

        public void LenghtCounter()
        {
            if(lenghtLoadCounter > 0 && !lenghtCounterHalt)
            {
                lenghtLoadCounter--;
            }
        }
        public void SweepCounter()
        {
           
            if(sweepPeriodCounter > 0)
            {
                if(--sweepPeriodCounter == 0 )
                {
                    sweepPeriodCounter = sweepPeriod;
                    if(validSweep && sweepShift > 0 && sweepEnabled)
                    {
                        int changeAmount = timer >> sweepShift;
                        timer = negate ? timer - changeAmount : timer + changeAmount;
                        CalculateValidSweep();
                    }
                    
                }
            }
            if (reloadSweep)
            {
                sweepPeriodCounter = sweepPeriod;
                reloadSweep = false;
            }
        }
        public void WriteReg(int address, byte data)
        {
            switch (address & 3)
            {
                case 0:
                    regs[0] = data;
                    duty = (data >> 6) & 3;
                    lenghtCounterHalt = ((data >> 5) & 1) != 0 ? true : false;
                    constantVolume = ((data >> 4) & 1) != 0 ? true : false;
                    volume = data & 0xF;
                    break;
                case 1:
                    regs[1] = data;
                    sweepEnabled = ((data >> 7) & 1) != 0 ? true : false;
                    sweepPeriod = (data >> 4) & 0x7;
                    negate = ((data >> 3) & 1) != 0 ? true : false;
                    sweepShift = data & 0x7;
                    reloadSweep = true;
                    break;
                case 2:
                    regs[2] = data;
                    timer &= ~0xFF;
                    timer |= data;
                    if (timer < 8) channelEnabled = false;
                    else channelEnabled = true;    
                    break;
                case 3:
                    regs[3] = data;
                    lenghtLoadCounter = lenghtCounterLookup[(data >> 3)];
                    timer &= ~0xFF00;
                    timer |= (data & 0x7) << 8;
                    if (timer < 8) channelEnabled = false;
                    else channelEnabled = true;  
                    dutyCounter = 0;
                    envelopeStart = true;
                    break;
            }
        }

    }
}
