using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class TriangleChannel
    {

        private byte[] regs = new byte[4];
        //$4008	CRRR.RRRR	Linear counter setup (write)

        private bool lenghtCounterHalt = false;
        private int linearCounter;



        //$400A	LLLL.LLLL	Timer low (write)
        private int timer;



        //$400B	llll.lHHH	Length counter load and timer high (write)
        private int lenghtLoadCounter;
        private bool linearCounterReload;

        private bool channelEnabled = false;

        private int timerCounter;
        private int linearStepCounter;
        private int triangleStep;

        private byte[] triangleWave = new byte[]
        {
            15, 14, 13, 12, 11, 10,  9,  8,  7,  6,  5,  4,  3,  2,  1,  0,
             0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15
        };

        private byte[] lenghtCounterLookup = new byte[]
        {
            10,254, 20,  2, 40,  4, 80,  6, 160,  8, 60, 10, 14, 12, 26, 14,
            12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30
        };

        public int Sample;

        public TriangleChannel() { }

        public void Tick()
        {
            if (timerCounter-- <= 0)
            {
                timerCounter = (timer / 2);
                if (triangleStep > triangleWave.Length - 1) triangleStep = 0;
                if (lenghtLoadCounter != 0 && linearStepCounter != 0 && channelEnabled)
                {
                    Sample = triangleWave[triangleStep++];
                }
                else Sample = 0;
                
                
            }
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

        public void LenghtCounter()
        {
            if (lenghtLoadCounter > 0 && !lenghtCounterHalt)
            {
                lenghtLoadCounter--;
            }
        }
        public void LinearCounter()
        {
            if (linearCounterReload) linearStepCounter = linearCounter;
            if(linearStepCounter > 0)
            {
                linearStepCounter--;
            }
            if (!lenghtCounterHalt) linearCounterReload = false;
        }
      
        public void WriteReg(int address, byte data)
        {
            switch (address & 3)
            {
                case 0:
                    regs[0] = data;
                    lenghtCounterHalt = ((data >> 5) & 7) != 0 ? true : false;
                    linearCounter = data & 0x7F;
                    break;
                case 1:
                    break;
                case 2:
                    regs[2] = data;
                    timer &= ~0xFF;
                    timer |= data;
                    if (timer < 2) channelEnabled = false;
                    else channelEnabled = true;
                    break;
                case 3:
                    regs[3] = data;
                    lenghtLoadCounter = lenghtCounterLookup[(data >> 3)];
                    timer &= ~0xFF00;
                    timer |= (data & 0x7) << 8;
                    if (timer < 2) channelEnabled = false;
                    else channelEnabled = true;
                    linearCounterReload = true;
                    break;
            }
        }
    }
}
