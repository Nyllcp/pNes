using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class DPCM
    {

        private Core _core;

        //$4010	IL--.RRRR	Flags and Rate (write)
        private bool interruptEnable = false;
        private bool loopFlag = false;
        private byte rate;

        //$4011	-DDD.DDDD Direct load(write)
        //private byte shiftRegister;
        private int sampleShiftCounter;

        //$4012	AAAA.AAAA Sample address(write) Sample address = %11AAAAAA.AA000000 = $C000 + (A * 64)
        private int sampleAdress = 0xC000;
        private int fetchAdress = 0xC000;
        private byte sampleBuffer;



        //$4013	LLLL.LLLL	Sample length (write) Sample length = %LLLL.LLLL0001 = (L * 16) + 1
        private int sampleLenght;
        public int sampleLenghtCounter;

        public int Sample = 0;
        public bool iFlag = false;

        private bool bufferAvailable = false;

        private int timerCounter;


        private int[] timerPeriodLookup = new int[]
        {
            428, 380, 340, 320, 286, 254, 226, 214, 190, 160, 142, 128, 106,  84,  72,  54
        };

        public DPCM(Core core) { _core = core; }

        public void Tick()
        {
            if(timerCounter-- <= 0)
            {
                timerCounter = timerPeriodLookup[rate] + 1;

                if (bufferAvailable)
                {
                    if ((sampleBuffer & 1) != 0)
                    {
                        if (Sample <= 125)
                        {
                            Sample += 2;
                        }
                    }
                    else
                    {
                        if (Sample >= 2)
                        {
                            Sample -= 2;
                        }
                    }
                    sampleBuffer >>= 1;
                }
                sampleShiftCounter--;
                if(sampleShiftCounter == 0)
                {
                    sampleShiftCounter = 8;
                    if (sampleLenghtCounter >= 0)
                    {
                        bufferAvailable = true;
                        sampleBuffer = _core.ReadMemory(fetchAdress);
                        if (fetchAdress >= 0xFFFF) fetchAdress = 0x8000;
                        else fetchAdress++;
                        sampleLenghtCounter--;
                        if(sampleLenghtCounter == 0)
                        {
                            if(loopFlag)
                            {
                                sampleLenghtCounter = sampleLenght;
                                fetchAdress = sampleAdress;
                            }
                            else
                            {
                                iFlag = interruptEnable;
                            }
                        }

                    }
                    else bufferAvailable = false;

                }

            }
        }

        public void EnableChannel(bool enable)
        {
            if(enable && sampleLenghtCounter <= 0)
            {
                fetchAdress = sampleAdress;
                sampleLenghtCounter = sampleLenght;
            }
            else
            {
                sampleLenghtCounter = 0;
                sampleLenght = 0;
            }
            iFlag = false;
        }

        public void WriteReg(int address, byte data)
        {
            switch (address & 3)
            {
                case 0:
                    interruptEnable = ((data >> 7) & 1) != 0;
                    loopFlag = ((data >> 6) & 1) != 0;
                    rate = (byte)(data & 0xF);
                    break;
                case 1:
                    Sample = data & 0x7F;
                    break;
                case 2:
                    sampleAdress = (data << 6) | 0xC000;
                    fetchAdress = sampleAdress;
                    break;
                case 3:
                    sampleLenght = (data << 4) | 1;
                    sampleLenghtCounter = sampleLenght;
                    break;
            }
        }
    }
}
