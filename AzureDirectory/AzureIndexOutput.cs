﻿using Lucene.Net.Support;
using Microsoft.Azure.Storage.Blob;
//using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;


namespace Lucene.Net.Store.Azure
{
    /// <summary>
    /// Implements IndexOutput semantics for a write/append only file
    /// </summary>
    public class AzureIndexOutput : IndexOutput
    {
        private AzureDirectory _azureDirectory;
        private CloudBlobContainer _blobContainer;
        private string _name;
        private IndexOutput _indexOutput;
        private Mutex _fileMutex;
        private ICloudBlob _blob;
        //private readonly CRC32 _crc;
        public Lucene.Net.Store.Directory CacheDirectory { get { return _azureDirectory.CacheDirectory; } }

        public AzureIndexOutput(AzureDirectory azureDirectory, ICloudBlob blob)
        {
            //_crc = new CRC32();

            _name = blob.Uri.Segments[blob.Uri.Segments.Length - 1];

            _fileMutex = BlobMutexManager.GrabMutex(_name); 
            _fileMutex.WaitOne();
            try
            {
                _azureDirectory = azureDirectory;
                _blobContainer = _azureDirectory.BlobContainer;
                _blob = blob;

                // create the local cache one we will operate against...
                _indexOutput = CacheDirectory.CreateOutput(_name,IOContext.DEFAULT);
            }
            finally
            {
                _fileMutex.ReleaseMutex();
            }
        }

        public override void Flush()
        {
            _indexOutput.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            _fileMutex.WaitOne();
            try
            {
                string fileName = _name;

                // make sure it's all written out
                _indexOutput.Flush();

                long originalLength = _indexOutput.Length;
                _indexOutput.Dispose();

                Stream blobStream;

                // optionally put a compressor around the blob stream
                if (_azureDirectory.ShouldCompressFile(_name))
                {
                    blobStream = CompressStream(fileName, originalLength);
                }
                else
                {
                    blobStream = new StreamInput(CacheDirectory.OpenInput(fileName,IOContext.DEFAULT));
                }

                try
                {
                    // push the blobStream up to the cloud
                    _blob.UploadFromStream(blobStream);

                    // set the metadata with the original index file properties
                    _blob.Metadata["CachedLength"] = originalLength.ToString();

                    var filePath = Path.Combine(_azureDirectory.CatalogPath, fileName);
                    var lastModified = File.GetLastWriteTimeUtc(filePath);
                    long fileTimeUtc = lastModified.ToFileTimeUtc();

                    //_blob.Metadata["CachedLastModified"] = CacheDirectory.FileModified(fileName).ToString();
                    _blob.Metadata["CachedLastModified"] = fileTimeUtc.ToString();
                    _blob.SetMetadata();

                    Debug.WriteLine(string.Format("PUT {1} bytes to {0} in cloud", _name, blobStream.Length));
                }
                finally
                {
                    blobStream.Dispose();
                }

#if FULLDEBUG
                Debug.WriteLine(string.Format("CLOSED WRITESTREAM {0}", _name));
#endif
                // clean up
                _indexOutput = null;
                _blobContainer = null;
                _blob = null;
                GC.SuppressFinalize(this);
            }
            finally
            {
                _fileMutex.ReleaseMutex();
            }
        }

        private MemoryStream CompressStream(string fileName, long originalLength)
        {
            // unfortunately, deflate stream doesn't allow seek, and we need a seekable stream
            // to pass to the blob storage stuff, so we compress into a memory stream
            MemoryStream compressedStream = new MemoryStream();

            try
            {
                using (var indexInput = CacheDirectory.OpenInput(fileName,IOContext.DEFAULT))
                using (var compressor = new DeflateStream(compressedStream, CompressionMode.Compress, true))
                {
                    // compress to compressedOutputStream
                    byte[] bytes = new byte[indexInput.Length];
                    indexInput.ReadBytes(bytes, 0, (int)bytes.Length);
                    compressor.Write(bytes, 0, (int)bytes.Length);
                }

                // seek back to beginning of comrpessed stream
                compressedStream.Seek(0, SeekOrigin.Begin);

                Debug.WriteLine(string.Format("COMPRESSED {0} -> {1} {2}% to {3}",
                   originalLength,
                   compressedStream.Length,
                   ((float)compressedStream.Length / (float)originalLength) * 100,
                   _name));
            }
            catch
            {
                // release the compressed stream resources if an error occurs
                compressedStream.Dispose();
                throw;
            }
            return compressedStream;
        }

        public override long Length
        {
            get
            {
                return _indexOutput.Length;
            }
        }

        public override void WriteByte(byte b)
        {
            _indexOutput.WriteByte(b);
        }

        public override void WriteBytes(byte[] b, int length)
        {
            _indexOutput.WriteBytes(b, length);
        }

        public override void WriteBytes(byte[] b, int offset, int length)
        {
            _indexOutput.WriteBytes(b, offset, length);
        }

        public /*override*/ long FilePointer
        {
            get
            {
                return _indexOutput.GetFilePointer();
            }
        }

        public override long Checksum => _indexOutput.Checksum;

        public override void Seek(long pos)
        {
            _indexOutput.Seek(pos);
        }

        public override long GetFilePointer()
        {
            return _indexOutput.GetFilePointer();
        }
    }
}