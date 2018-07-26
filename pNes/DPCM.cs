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

        private bool channelEnabled = false;

        //$4010	IL--.RRRR	Flags and Rate (write)
        private bool interruptEnable = false;
        private bool loopFlag = false;
        private byte rate;

        //$4011	-DDD.DDDD Direct load(write)
        //private byte shiftRegister;
        private int sampleShiftCounter;

        //$4012	AAAA.AAAA Sample address(write) Sample address = %11AAAAAA.AA000000 = $C000 + (A * 64)
        private int sampleAdress;
        private int fetchAdress;
        private byte sampleBuffer;



        //$4013	LLLL.LLLL	Sample length (write) Sample length = %LLLL.LLLL0001 = (L * 16) + 1
        private int sampleLenght;
        public int sampleLenghtCounter;

        public int Sample;
        public bool iFlag = false;

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
                if (sampleShiftCounter == 7)
                {
                    sampleShiftCounter = 0;
                    if (sampleLenghtCounter != 0)
                    {
                        fetchAdress &= 0x7FFF + 0x8000;
                        channelEnabled = false;
                        sampleBuffer = _core.ReadMemory(fetchAdress);
                        fetchAdress++;
                        sampleLenghtCounter--;
                        if (sampleLenghtCounter == 0)
                        {
                            if (loopFlag)
                            {
                                fetchAdress = sampleAdress;
                                sampleLenghtCounter = sampleLenght;
                            }
                            else
                            {
                                iFlag = interruptEnable;
                            }
                        }

                    }
                   
                }
                timerCounter = timerPeriodLookup[rate] / 2;
                if(channelEnabled)
                {
                    Sample = ((sampleBuffer >> sampleShiftCounter++) & 1) != 0 ? Sample + 2 : Sample - 2;
                    if (Sample > 127) Sample = 127;
                    if (Sample < 0) Sample = 0;
                }
                     
                

            }
        }

        public void EnableChannel(bool enable)
        {
            channelEnabled = enable;
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
