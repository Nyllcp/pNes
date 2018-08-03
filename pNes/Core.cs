using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Core
    {
        private Cartridge _cart;
        //private Cpu _cpu;
        private Processor _cpu;
        private Ppu _ppu;
        private Apu _apu;

        private byte pad1;
        private bool strobingPad = false;
        private int padShiftCounter = 0;

        private int _lastCycles;

        private byte[] ram = new byte[0x800];

        public uint[] Frame { get { return _ppu.Frame; } }
        public byte Pad1 { get { return pad1; } set { pad1 = value; } }

        public byte[] Samples { get { return _apu.Samples; } }
        public int NoOfSamples { get { int value = _apu.NumberOfSamples; _apu.NumberOfSamples = 0; return value; } }
        

        public Core()
        {
            _cart = new Cartridge();
            _cpu = new Processor(this);
            _ppu = new Ppu(_cart,this);
            _apu = new Apu(this);
        }

        public bool LoadRom(Rom rom)
        {
            bool value = _cart.LoadRom(rom);
            if (value) _cpu.Reset();
            return value;
        }

        public void RunOneFrame()
        {
            _lastCycles = _cpu.GetCycleCount();
            while(!_ppu.FrameReady)
            {
                MachineCycle();
            }
            int cyclesPerFrame = _cpu.GetCycleCount() - _lastCycles;
        }

        public void MachineCycle()
        {
            _ppu.Tick();  
            _ppu.Tick();
            _ppu.Tick();
            _apu.Tick();
            if (_apu.IFlag) _cpu.InterruptRequest();
            _cart.Tick();
            _cpu.NextStep();    
        }

        public void NonMaskableIntterupt()
        {
            _cpu.TriggerNmi = true;
            //_cpu.NonMaskableInterrupt();
        }

        //Address range   Size Device
        //$0000-$07FF	$0800	2KB internal RAM
        //$0800-$0FFF	$0800	Mirrors of $0000-$07FF
        //$1000-$17FF	$0800
        //$1800-$1FFF	$0800
        //$2000-$2007	$0008	NES PPU registers
        //$2008-$3FFF	$1FF8 Mirrors of $2000-2007 (repeats every 8 bytes)
        //$4000-$4017	$0018	NES APU and I/O registers
        //$4018-$401F	$0008	APU and I/O functionality that is normally disabled.See CPU Test Mode.
        //$4020-$FFFF $BFE0 Cartridge space: PRG ROM, PRG RAM, and mapper registers (See Note)

        public byte ReadMemory(int address)
        {
            address &= 0xFFFF;
            if (address < 0x2000)
            {
                return ram[address & 0x7FF];
            }
            else if(address < 0x4000)
            {
                return _ppu.ReadPpuRegister(address);
            }
            else if(address < 0x4020)
            {
                if (address == 0x4016)
                {
                    int val = 0;
                    if (strobingPad) val = pad1 & 1;
                    else
                    {
                        val = (pad1 >> padShiftCounter++) & 1;
                    }
                    return (byte)(val |= 0x40);
                }
                else return _apu.ReadApuRegister(address);
            
            }
            else
            {
                //$4020-$FFFF $BFE0 Cartridge space: PRG ROM, PRG RAM, and mapper registers (See Note)
                return _cart.ReadCart(address);
            }
        }

        public void WriteMemory(int address, byte data)
        {
            address &= 0xFFFF;
            if (address < 0x2000)
            {
                ram[address & 0x7FF] = data;
            }
            else if (address < 0x4000)
            {
                _ppu.WritePpuRegister(address, data);
            }
            else if(address < 0x4020)
            {
                //$4000-$4017	$0018	NES APU and I/O registers
                //$4018-$401F	$0008	APU and I/O functionality that is normally disabled.See CPU Test Mode.
                if (address == 0x4014)
                {
                    _cpu.CycleCountStep += 514; //oam transfer stall cpu for 514 cycles
                    _ppu.WritePpuRegister(address, data);
                }
                else if (address == 0x4016)
                {
                    strobingPad = ((data & 1) != 0) ? true : false;
                    padShiftCounter = 0;
                }
                else
                {
                    _apu.WriteApuRegister(address,data);
                }

                
            }
            else
            {
                //$4020-$FFFF $BFE0 Cartridge space: PRG ROM, PRG RAM, and mapper registers (See Note)
                _cart.WriteCart(address, data);
            }
        }


      
    }
}
