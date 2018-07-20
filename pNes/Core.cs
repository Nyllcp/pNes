using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Core
    {
        private Cart _cart;
        private Cpu _cpu;
        private Ppu _ppu;

        private byte[] ram = new byte[0x800];

        public uint[] Frame { get { return _ppu.Frame; } }

        public Core()
        {
            _cart = new Cart();
            _cpu = new Cpu(this);
            _ppu = new Ppu(_cart,this);
        }

        public bool LoadRom(string fileName)
        {
            bool value = _cart.LoadRom(fileName);
            if (value) _cpu.Reset();
            return value;
        }

        public void RunOneFrame()
        {
            while(!_ppu.FrameReady)
            {
                MachineCycle();
            }
        }

        public void MachineCycle()
        {
            _ppu.Tick();
            if (_ppu.CheckNMI()) _cpu.NonMaskableInterrupt();
            _ppu.Tick();
            _ppu.Tick();
            _cpu.NextStep();
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
                //$4000-$4017	$0018	NES APU and I/O registers
                //$4018-$401F	$0008	APU and I/O functionality that is normally disabled.See CPU Test Mode.
                return 0;
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
            if(address < 0x2000)
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
                if (address == 0x4014) _ppu.WritePpuRegister(address, data); 
            }
            else
            {
                //$4020-$FFFF $BFE0 Cartridge space: PRG ROM, PRG RAM, and mapper registers (See Note)
            }
        }


      
    }
}
