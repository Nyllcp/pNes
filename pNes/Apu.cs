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
        const int SAMPLE_RATE = 44100;

        private int apuCycles;
        private int sampleCycles;

        public byte[] Samples = new byte[2048]; // Abit more then a frame worth of samples. 44100 / 60 = 735. 2 bytes per sample 1470 bytes..
        public int NumberOfSamples = 0;

        private Int16[] _pulseLookup;
        private Int16[] _tndLookup;

        private PulseChannel _pulseChannel0;
        private PulseChannel _pulseChannel1;
        private TriangleChannel _triangleChannel;
        private NoiseChannel _noiseChannel;
        private DPCM _dpcm;

        private bool iFlag = false;
        private bool triggerChannels = false;
        private bool apuEveryOtherCycle = false;


        private bool mode0 = true;
        private bool disableInterrupt = true;

        public bool IFlag { get { bool value = iFlag || _dpcm.dpcmiFlag; iFlag = false; return value; } }

        public Apu(Core core)
        {
            _pulseChannel0 = new PulseChannel(true);
            _pulseChannel1 = new PulseChannel(false);
            _triangleChannel = new TriangleChannel();
            _noiseChannel = new NoiseChannel();
            _dpcm = new DPCM(core);
            SetupLookupTables();
        }

        

        public void Tick()
        {
            if(apuEveryOtherCycle)
            {
                FrameSequencer();
                _pulseChannel0.Tick();
                _pulseChannel1.Tick();
                _noiseChannel.Tick();  
            }
            
            _triangleChannel.Tick();
            _dpcm.Tick();
            apuEveryOtherCycle = !apuEveryOtherCycle;
            sampleCycles++;
            if (sampleCycles >= (cpuFreq) / SAMPLE_RATE) Sample();
        }

        private void Sample()
        {
            sampleCycles = 0;
            if (NumberOfSamples * 2 > Samples.Length - 2) return;
            int sample = 0;
            int pulseOut = _pulseLookup[_pulseChannel0.Sample + _pulseChannel1.Sample];
            int tndOut = _tndLookup[(_triangleChannel.Sample * 3) + (_noiseChannel.Sample * 2) + _dpcm.Sample ]; // [3 * triangle + 2 * noise + dmc]

            sample = pulseOut + tndOut;

            Samples[NumberOfSamples * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            Samples[NumberOfSamples * 2] = (byte)(sample & 0xFF);
            NumberOfSamples++;
        }

        private void FrameSequencer()
        {
            if(mode0)
            {
                if(apuCycles % 3728 == 0)
                {
                    //Envelopes & triangle's linear counter (Quarter frame)	240hz
                    _triangleChannel.LinearCounter();
                    _pulseChannel0.EnvelopeCounter();
                    _pulseChannel1.EnvelopeCounter();
                    _noiseChannel.EnvelopeCounter();
                    
                }
                if(apuCycles % 7456 == 0)
                {
                    //Length counters &sweep units(Half frame) 120hz
                    _noiseChannel.LenghtCounter();
                    _triangleChannel.LenghtCounter();
                    _pulseChannel0.LenghtCounter();
                    _pulseChannel1.LenghtCounter();
                    _pulseChannel0.SweepCounter();
                    _pulseChannel1.SweepCounter();
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
                    _triangleChannel.LinearCounter();
                    _pulseChannel0.EnvelopeCounter();
                    _pulseChannel1.EnvelopeCounter();
                    _noiseChannel.EnvelopeCounter();
                }
                if (apuCycles == 7465 || apuCycles == 18640)
                {
                    //Length counters &sweep units(Half frame) 	96 Hz (approx.), uneven timing
                    _noiseChannel.LenghtCounter();
                    _triangleChannel.LenghtCounter();
                    _pulseChannel0.LenghtCounter();
                    _pulseChannel1.LenghtCounter();
                    _pulseChannel0.SweepCounter();
                    _pulseChannel1.SweepCounter();
                }
                if (apuCycles > 18640) apuCycles = 0;
            }
            if(triggerChannels)
            {
                triggerChannels = false;
                _triangleChannel.LinearCounter();
                _pulseChannel0.EnvelopeCounter();
                _pulseChannel1.EnvelopeCounter();
                _noiseChannel.EnvelopeCounter();

                _noiseChannel.LenghtCounter();
                _triangleChannel.LenghtCounter();
                _pulseChannel0.LenghtCounter();
                _pulseChannel1.LenghtCounter();
                _pulseChannel0.SweepCounter();
                _pulseChannel1.SweepCounter();
            }
            apuCycles++;

        }

        public void WriteApuRegister(int address, byte data)
        {
            address &= 0xFF;
            switch(address)
            {
                case 0x0:
                case 0x1:
                case 0x2:
                case 0x3:_pulseChannel0.WriteReg(address, data); break;
                case 0x4:
                case 0x5:
                case 0x6:
                case 0x7: _pulseChannel1.WriteReg(address, data); break;
                case 0x8:
                case 0x9:
                case 0xA:
                case 0xB: _triangleChannel.WriteReg(address, data); break;
                case 0xC:
                case 0xD:
                case 0xE:
                case 0xF: _noiseChannel.WriteReg(address, data); break;
                case 0x10:
                case 0x11:
                case 0x12:
                case 0x13: _dpcm.WriteReg(address, data); break;
                case 0x15:
                    _pulseChannel0.EnableChannel(((data & 1) != 0));
                    _pulseChannel1.EnableChannel((((data >> 1) & 1) != 0));
                    _triangleChannel.EnableChannel((((data >> 2) & 1) != 0));
                    _noiseChannel.EnableChannel((((data >> 3) & 1) != 0));
                    _dpcm.EnableChannel(((data >> 4) & 1) != 0);
                    break;
                case 0x17:
                    mode0 = ((data >> 7) & 1) != 0 ? false : true;
                    disableInterrupt = ((data >> 6) & 1) != 0 ? true : false;
                    apuCycles = 0;
                    if (disableInterrupt) iFlag = false;
                    if (!mode0) triggerChannels = true;
                    break;
            }
        }

        public byte ReadApuRegister(int address)
        {
            address &= 0xFF;
            switch (address)
            {
                case 0x15:
                    int value = _dpcm.dpcmiFlag == true ? 1 << 7 : 0;
                    value |= iFlag == true ? 1 << 6 : 0;
                    iFlag = false;
                    value |= (_dpcm.sampleLenghtCounter > 0) ? 1 << 4 : 0;
                    value |= _noiseChannel.LenghtCounterNotZero() ? 1 << 3 : 0;
                    value |= _triangleChannel.LenghtCounterNotZero() ? 1 << 2 : 0;
                    value |= _pulseChannel1.LenghtCounterNotZero() ? 1 << 1 : 0;
                    value |= _pulseChannel0.LenghtCounterNotZero() ? 1 : 0;

                    return (byte)value;
            }
            return 0;
        }

        private void SetupLookupTables()
        {
            _pulseLookup = new Int16[0x1F];
            _tndLookup = new Int16[0xCB];
            for (int i = 0; i < _pulseLookup.Length; i++)
            {
                _pulseLookup[i] = (Int16)((95.52 / (8128.0 / i + 100)) * (float)Int16.MaxValue);
            }
            for (int i = 0; i < _tndLookup.Length; i++)
            {
                _tndLookup[i] = (Int16)((163.67 / (24329.0 / i + 100)) * (float)Int16.MaxValue);
            }
        }

        public void WriteSaveState(ref Savestate state)
        {
            state.apuCycles = apuCycles;
            state.sampleCycles = sampleCycles;
            state.iFlag = iFlag;
            state.mode0 = mode0;
            state.disableInterrupt = disableInterrupt;
            state.apuEveryOtherCycle = apuEveryOtherCycle;
        }
        public void LoadSaveState(ref Savestate state)
        {
            apuCycles = state.apuCycles;
            sampleCycles = state.sampleCycles;
            iFlag = state.iFlag;
            mode0 = state.mode0;
            disableInterrupt = state.disableInterrupt;
            apuEveryOtherCycle = state.apuEveryOtherCycle;
        }
    }

 
}
