using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pNes
{
    class Cartridge
    {

        private Mapper _mapper;
        public bool Iflag { get { return _mapper.Iflag; } }

        public Cartridge() { }

        public bool LoadRom(Rom rom)
        {
            switch(rom.mapperNumber)
            {
                case 0: _mapper = new Mapper0(); _mapper.Init(rom); break;
                case 1: _mapper = new Mapper1(); _mapper.Init(rom); break;
                case 2: _mapper = new Mapper2(); _mapper.Init(rom); break;
                case 3: _mapper = new Mapper3(); _mapper.Init(rom); break;
                case 4: _mapper = new Mapper4(); _mapper.Init(rom); break;
            }

            return _mapper != null;
        }

        public void WriteCart(int address, byte data)
        {
            _mapper.WriteCart(address, data);
        }

        public byte ReadCart(int address)
        {
            return _mapper.ReadCart(address);
        }

        public void Tick()
        {
            _mapper.Tick();
        }
    }
}
