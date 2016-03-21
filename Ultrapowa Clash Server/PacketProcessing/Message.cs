﻿using Sodium;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UCS.Helpers;
using UCS.Logic;

namespace UCS.PacketProcessing
{
    internal class Message
    {
        private byte[] m_vData;
        private int m_vLength;
        private ushort m_vMessageVersion;
        private ushort m_vType;
       
        public Message() { }

        public Message(Client c)
        {
            Client = c;
            m_vType = 0;
            m_vLength = -1;
            m_vMessageVersion = 0;
            m_vData = null;
        }

        public Message(Client c, BinaryReader br)
        {
            Client = c;
            m_vType = br.ReadUInt16WithEndian();
            var tempLength = br.ReadBytes(3);
            m_vLength = (0x00 << 24) | (tempLength[0] << 16) | (tempLength[1] << 8) | tempLength[2];
            m_vMessageVersion = br.ReadUInt16WithEndian();
            m_vData = br.ReadBytes(m_vLength);
        }
        private static void IncrementNonce(byte[] nonce)
        {
            // TODO: Write own method for incrementing nonces by 2.
            nonce = Utilities.Increment(Utilities.Increment(nonce));
        }
        public void Encrypt8(byte[] plainText)
        {
            if (m_vType == 20103)
            {
                byte[] nonce = GenericHash.Hash(Client.CSNonce.Concat(Client.CPublicKey).Concat(Crypto8.StandardKeyPair.PublicKey).ToArray(), null, 24);
                plainText = Client.CSNonce.Concat(Client.CSharedKey).Concat(plainText).ToArray();
                SetData(PublicKeyBox.Create(plainText, nonce, Crypto8.StandardKeyPair.PrivateKey, Client.CPublicKey));
                Client.CState = 2;
            }
            else if (m_vType == 20104)
            {
                byte[] nonce = GenericHash.Hash(Client.CSNonce.Concat(Client.CPublicKey).Concat(Crypto8.StandardKeyPair.PublicKey).ToArray(), null, 24);
                plainText = Client.CSNonce.Concat(Client.CSharedKey).Concat(plainText).ToArray();
                SetData(PublicKeyBox.Create(plainText, nonce, Crypto8.StandardKeyPair.PrivateKey, Client.CPublicKey));
                Client.CState = 2;
            }
            else 
            {
                IncrementNonce(Client.CSNonce);
                SetData(SecretBox.Create(plainText, Client.CSNonce, Client.CSharedKey).Skip(16).ToArray());
            }
        }
        public void Decrypt8()
        {
            if (m_vType == 10101)
            {
                byte[] cipherText = m_vData;
                Client.CPublicKey = cipherText.Take(32).ToArray();
                Client.CSharedKey = Client.CPublicKey;
                Client.CRNonce = Crypto8.GenerateNonce();
                byte[] nonce = GenericHash.Hash(Client.CPublicKey.Concat(Crypto8.StandardKeyPair.PublicKey).ToArray(), null, 24);
                cipherText = cipherText.Skip(32).ToArray();
                var PlainText = PublicKeyBox.Open(cipherText, nonce, Crypto8.StandardKeyPair.PrivateKey, Client.CPublicKey);
                Client.CSessionKey = PlainText.Take(24).ToArray();
                Client.CSNonce = PlainText.Skip(24).Take(24).ToArray();
                SetData(PlainText.Skip(24).Skip(24).ToArray());
                Client.CState = 1;
            }
            else
            {
                Client.CSNonce = Utilities.Increment(Utilities.Increment(Client.CSNonce));
                SetData(SecretBox.Open(new byte[16].Concat(m_vData).ToArray(), Client.CSNonce, Client.CSharedKey));
            }
        }

        public int Broadcasting { get; set; }

        public Client Client { get; set; }

        public virtual void Decode() { }

        public virtual void Encode() { }

        public byte[] GetData()
        {
            return m_vData;
        }

        public int GetLength()
        {
            return m_vLength;
        }

        public ushort GetMessageType()
        {
            return m_vType;
        }

        public ushort GetMessageVersion()
        {
            return m_vMessageVersion;
        }

        public byte[] GetRawData()
        {
            var encodedMessage = new List<byte>();
            encodedMessage.AddRange(BitConverter.GetBytes(m_vType).Reverse());
            encodedMessage.AddRange(BitConverter.GetBytes(m_vLength).Reverse().Skip(1));
            encodedMessage.AddRange(BitConverter.GetBytes(m_vMessageVersion).Reverse());
            encodedMessage.AddRange(m_vData);
            return encodedMessage.ToArray();
        }

        public virtual void Process(Level level) { }

        public void SetData(byte[] data)
        {
            m_vData = data;
            m_vLength = data.Length;
        }

        public void SetMessageType(ushort type)
        {
            m_vType = type;
        }

        public void SetMessageVersion(ushort v)
        {
            m_vMessageVersion = v;
        }

        public string ToHexString()
        {
            var hex = BitConverter.ToString(m_vData);
            return hex.Replace("-", " ");
        }

        public override string ToString()
        {
            return Encoding.UTF8.GetString(m_vData, 0, m_vLength);
        }
    }
}