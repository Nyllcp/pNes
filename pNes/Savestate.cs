using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    [Serializable()]
    class Savestate
    {
        public SoundChannelState _pulse0;
        public SoundChannelState _pulse1;
        public SoundChannelState _noise;
        public SoundChannelState _dpcm;

        public Savestate()
        {
            _pulse0 = new SoundChannelState();
            _pulse1 = new SoundChannelState();
            _noise = new SoundChannelState();
            _dpcm = new SoundChannelState();
        }


        //core states
        public byte[] ram = new byte[0x800];

        //ppu states
        public byte[] oam = new byte[0x100];
        public SpriteObject[] sOam = new SpriteObject[8];
        public byte[] scanlineBuffer = new byte[256];
        public byte[] spriteScanlineBuffer = new byte[256];
        public byte[] paletteRam = new byte[0x20];
        public byte oamAddr;
        public byte lastWritten;
        public byte readBuffer;
        public byte tileAttribute;
        public byte bufferTileAttribute;
        public int tileData0;
        public int tileData1;
        public int tilePointer;
        public int ppuAddress;
        public int tempPpuAddress;
        public int spriteTableAddress = 0x0000;
        public int backgroundTableAdress = 0x0000;
        public int vramAddressIncrement = 1;
        public int ppuCycles = 0;
        public int currentScanline = 0;
        public int fineX;
        public int currentDot;
        public bool largeSprites = false;
        public bool vblank_NMI = false;
        public bool grayScale = false;
        public bool showLeftBg = true;
        public bool showLeftSprite = true;
        public bool bgEnabled = false;
        public bool spritesEnabled = false;
        public bool spriteOverflow = false;
        public bool sprite0Hit = false;
        public bool inVblank = false;
        public bool addressLatch = false;
        public bool oddFrame = false;
        public bool frameReady = false;

        //Cpu States

        public int _programCounter;
        public int _stackPointer;
        public int _cycleCount;
        public int _cycleCountStep;
        public bool _previousInterrupt;
        public bool _interrupt;
        public int Accumulator { get;  set; }
        public int XRegister { get;  set; }
        public int YRegister { get;  set; }
        public int CurrentOpCode { get;  set; }
        public bool CarryFlag { get;  set; }
        public bool ZeroFlag { get;  set; }
        public bool DisableInterruptFlag { get; set; }
        public bool DecimalFlag { get; set; }
        public bool OverflowFlag { get; set; }
        public bool NegativeFlag { get; set; }
        public bool TriggerNmi { get; set; }
        public bool TriggerIRQ { get; set; }

        //apu states
        public int apuCycles;
        public int sampleCycles;
        public byte[] Samples = new byte[2048]; // Abit more then a frame worth of samples. 44100 / 60 = 735. 2 bytes per sample 1470 bytes..
        public int NumberOfSamples = 0;
        public bool iFlag = false;
        public bool triggerChannels = false;
        public bool apuEveryOtherCycle = false;
        public bool mode0 = true;
        public bool disableInterrupt = true;

        //cart states
        public byte[,] ppuRam = new byte[4, 0x400];
        public byte[] prgRam = new byte[0x2000];
        public byte[] chrRom = new byte[0x2000];
        public bool iflag = false;
        public bool verticalMirroring = false;

        //mapper1
        public byte loadRegister;
        public byte shiftCount;
        public int currentMirroring;
        public byte prgRomBankMode = 3; // cart set ctrl reg to 0x0C at startup, resulting in bankmode 3.
        public byte chrRomBankMode = 0;
        public byte chrBank0;
        public byte chrBank1;
        public byte prgBank;
        public bool prgRamEnabled;

        //mapper2
        public int prgBankNo;

        //mapper3
        public int chrBankNo;

        //mapper4
        public int[] bankRegigster = new int[8];
        public bool prgBankMode8000 = false;
        public bool chr2kHigh = false;
        public int bankWriteSelect;

        public bool irqEnabled = false;
        public int irqLatch = 0xFF;
        public int irqCounter;
        public int lastAddress;

        //mapper 7
        public int nameTable;

        public class SoundChannelState
        {

            //Shared variables
            public bool lenghtCounterHalt = false;
            public bool constantVolume = false;
            public int volume;
            public bool lenghtEnable = false;
            public int lenghtLoadCounter;
            public bool envelopeStart = false;
            public int timerCounter;
            public int sweepPeriodCounter;
            public int envelopeCounter;
            public int envelopeVolume;
            public int currentVolume;

            //dpcm
            public bool interruptEnable = false;
            public bool loopFlag = false;
            public byte rate;
            public int sampleShiftCounter;
            public int sampleAdress = 0xC000;
            public int fetchAdress = 0xC000;
            public byte sampleBuffer;
            public int sampleLenght;
            public int sampleLenghtCounter;
            public int Sample = 0;
            public bool dpcmiFlag = false;
            public bool bufferAvailable = false;

            //noise
            public bool mode1 = false;
            public byte period;
            public int shiftRegister = 1;
            public int noisetimerCounter;

            //pulse
            public int duty;
            public bool sweepEnabled = false;
            public int sweepPeriod;
            public bool negate = false;
            public int sweepShift;
            public int timer;
            public bool reloadSweep = false;
            public bool validSweep = true;
            public bool isChannel0 = false;

        }
    }
}
