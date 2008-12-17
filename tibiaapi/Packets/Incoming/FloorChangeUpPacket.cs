﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tibia.Packets.Incoming
{
    public class FloorChangeUpPacket : IncomingPacket
    {
        NetworkMessage stream = new NetworkMessage(0);
        private short m_skipTiles;

        private List<Objects.Item> items = new List<Tibia.Objects.Item> { };
        private List<PacketCreature> creatures = new List<PacketCreature> { };

        public List<Objects.Item> Items
        {
            get { return items; }
        }

        public List<PacketCreature> Creatures
        {
            get { return creatures; }
        }

        public FloorChangeUpPacket(Objects.Client c)
            : base(c)
        {
            Type = IncomingPacketType_t.FLOOR_CHANGE_UP ;
            Destination = PacketDestination_t.CLIENT;
        }

        public override bool ParseMessage(NetworkMessage msg, PacketDestination_t destination, Objects.Location pos)
        {
            if (msg.GetByte() != (byte)IncomingPacketType_t.FLOOR_CHANGE_UP)
                return false;

            Destination = destination;
            Type = IncomingPacketType_t.FLOOR_CHANGE_UP;
            stream.AddByte((byte)Type);

            pos.Z--;
            //going to surface
            if (pos.Z == 7)
            {
                //floor 7 and 6 already set
                for (int i = 5; i >= 0; i--)
                {
                    if (!setFloorDescription(msg, pos.X - 8, pos.Y - 6, i, 18, 14, 8 - i))
                    {
                        //RAISE_PROTOCOL_ERROR("Set Floor Desc z = 7 0xBE");
                    }
                }
            }
            //underground, going one floor up (still underground)
            else if (pos.Z > 7)
            {
                if (!setFloorDescription(msg, pos.X - 8, pos.Y - 6, pos.Z - 2, 18, 14, 3))
                {
                    //RAISE_PROTOCOL_ERROR("Set Floor Desc  z > 7 0xBE");
                }
            }
            pos.X++;
            pos.Y++;

            return true;
        }

        private bool setMapDescription(NetworkMessage msg, int x, int y, int z, int width, int height)
        {
            int startz, endz, zstep;
            //calculate map limits
            if (z > 7)
            {
                startz = z - 2;
                endz = System.Math.Min(16 - 1, z + 2);
                zstep = 1;
            }
            else
            {
                startz = 7;
                endz = 0;
                zstep = -1;
            }

            for (int nz = startz; nz != endz + zstep; nz += zstep)
            {
                //pare each floor
                if (!setFloorDescription(msg, x, y, nz, width, height, z - nz))
                    return false;
            }

            return true;
        }

        private bool setFloorDescription(NetworkMessage msg, int x, int y, int z, int width, int height, int offset)
        {
            ushort skipTiles;

            for (int nx = 0; nx < width; nx++)
            {
                for (int ny = 0; ny < height; ny++)
                {
                    if (m_skipTiles == 0)
                    {
                        ushort tileOpt = msg.PeekUInt16();
                        //Decide if we have to skip tiles
                        // or if it is a real tile
                        if (tileOpt >= 0xFF00)
                        {
                            skipTiles = msg.GetUInt16();
                            stream.AddUInt16(skipTiles);

                            m_skipTiles = (short)(skipTiles & 0xFF);
                        }
                        else
                        {
                            //real tile so read tile
                            Objects.Location pos = new Tibia.Objects.Location(x + nx + offset, y + ny + offset, z);

                            if (!setTileDescription(msg, pos))
                            {
                                return false;
                            }
                            //read skip tiles info
                            skipTiles = msg.GetUInt16();
                            stream.AddUInt16(skipTiles);

                            m_skipTiles = (short)(skipTiles & 0xFF);
                        }
                    }
                    //skipping tiles...
                    else
                    {
                        m_skipTiles--;
                    }
                }
            }
            return true;
        }

        private bool setTileDescription(NetworkMessage msg, Objects.Location pos)
        {
            int n = 0;
            while (true)
            {
                //avoid infinite loop
                n++;

                ushort inspectTileId = msg.PeekUInt16();

                if (inspectTileId >= 0xFF00)
                {
                    //end of the tile
                    return true;
                }
                else
                {
                    if (n > 10)
                    {
                        return false;
                    }
                    //read tile things: items and creatures
                    internalGetThing(msg);
                }
            }
        }

        private bool internalGetThing(NetworkMessage msg)
        {
            ushort thingId = msg.GetUInt16();
            stream.AddUInt16(thingId);

            PacketCreature c;

            if (thingId == 0x0061 || thingId == 0x0062)
            {

                c = new PacketCreature(Client);

                if (thingId == 0x0062)
                {
                    c.Type = PacketCreatureType_t.KNOW;
                    c.Id = msg.GetUInt32();
                    stream.AddUInt32(c.Id);
                }
                else if (thingId == 0x0061)
                {
                    c.RemoveId = msg.GetUInt32();
                    stream.AddUInt32(c.RemoveId);

                    c.Type = PacketCreatureType_t.UNKNOW;
                    c.Id = msg.GetUInt32();
                    stream.AddUInt32(c.Id);

                    c.Name = msg.GetString();
                    stream.AddString(c.Name);
                }

                c.Health = msg.GetByte();
                stream.AddByte(c.Health);

                c.Direction = msg.GetByte();
                stream.AddByte(c.Direction);

                c.Outfit = msg.GetOutfit();
                stream.AddOutfit(c.Outfit);

                c.LightLevel = msg.GetByte();
                stream.AddByte(c.LightLevel);

                c.LightColor = msg.GetByte();
                stream.AddByte(c.LightColor);

                c.Speed = msg.GetUInt16();
                stream.AddUInt16(c.Speed);

                c.Skull = (Constants.Skulls_t)msg.GetByte();
                stream.AddByte((byte)c.Skull);

                c.PartyShield = (PartyShields_t)msg.GetByte();
                stream.AddByte((byte)c.PartyShield);

                creatures.Add(c);

                return true;
            }
            else if (thingId == 0x0063)
            {
                //creature turn
                c = new PacketCreature(Client);
                c.Type = PacketCreatureType_t.TURN;

                c.Id = msg.GetUInt32();
                stream.AddUInt32(c.Id);

                c.Direction = msg.GetByte();
                stream.AddByte(c.Direction);

                creatures.Add(c);

                return true;
            }
            else
            {
                Objects.Item item = new Tibia.Objects.Item(Client, thingId, 0);

                if (item.HasExtraByte)
                {
                    item.Count = msg.GetByte();
                    stream.AddByte(item.Count);
                }

                items.Add(item);

                return true;
            }
        }

        public override byte[] ToByteArray()
        {
            return stream.Packet;
        }
    }
}