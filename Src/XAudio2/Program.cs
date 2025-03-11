using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Vortice;
using Vortice.Multimedia;
using Vortice.XAudio2;

Stopwatch stopwatch = new Stopwatch();
stopwatch.Start();


var filePath = "TownTheme.wav";
ReadOnlySpan<byte> file = File.ReadAllBytes(filePath);
int index = 0;

Console.WriteLine($"File name: {filePath}");
Console.WriteLine("----");
Console.WriteLine("File type: wav");
Console.WriteLine("----");

if (file[index++] != 'R' || file[index++] != 'I' || file[index++] != 'F' || file[index++] != 'F')
{
    Console.WriteLine("Given file is not in RIFF format");
    return;
}

var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
index += 4;

if (file[index++] != 'W' || file[index++] != 'A' || file[index++] != 'V' || file[index++] != 'E')
{
    Console.WriteLine("Given file is not in WAVE format");
    return;
}

short numChannels = -1;
int sampleRate = -1;
int byteRate = -1;
short blockAlign = -1;
short bitsPerSample = -1;
DataStream pcmDataStream = null;

while (index + 4 < file.Length)
{
    var identifier = "" + (char)file[index++] + (char)file[index++] + (char)file[index++] + (char)file[index++];
    var size = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
    index += 4;

    if (identifier == "fmt ")
    {
        if (size != 16)
        {
            Console.WriteLine($"Unknown Audio Format with subchunk1 size {size}");
        }
        else
        {
            var audioFormat = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
            index += 2;
            if (audioFormat != 1)
            {
                Console.WriteLine($"Unknown Audio Format with ID {audioFormat}");
            }
            else
            {
                numChannels = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                index += 2;
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
                index += 4;
                byteRate = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
                index += 4;
                blockAlign = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                index += 2;
                bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                index += 2;
            }
        }
    }
    else if (identifier == "data")
    {
        var data = file.Slice(index, size);
        index += size;
        pcmDataStream = DataStream.Create(data.ToArray(), true, true);
        Console.WriteLine($"Read {size} bytes Data");
    }
    else if (identifier == "JUNK")
    {
        index += size;
    }
    else if (identifier == "iXML")
    {
        var v = file.Slice(index, size);
        var str = Encoding.ASCII.GetString(v);
        Console.WriteLine($"iXML Chunk: {str}");
        index += size;
    }
    else
    {
        Console.WriteLine($"Unknown Section: {identifier}");
        index += size;
    }
}

if (pcmDataStream == null)
{
    Console.WriteLine("No valid data chunk found in the WAV file.");
    return;
}




stopwatch.Stop();
Console.WriteLine($"ElapsedMilliseconds: {stopwatch.ElapsedMilliseconds} ms");


var waveFormat = new WaveFormatExtensible(sampleRate, bitsPerSample, numChannels);

// Create the XAudio2 engine.
using IXAudio2 audio = XAudio2.XAudio2Create(ProcessorSpecifier.DefaultProcessor);
// Create mastering voice.
using IXAudio2MasteringVoice masterVoice = audio.CreateMasteringVoice();
// Create source voice for buffer submission.
using IXAudio2SourceVoice voice = audio.CreateSourceVoice(waveFormat, false);

// Load sound effect into audio buffer.
using AudioBuffer effectBuffer = new(pcmDataStream);

// Play buffer.
voice.SubmitSourceBuffer(effectBuffer, null);
voice.Start(0);

Console.SetCursorPosition(0, 0);
Console.WriteLine($"Playing sound effect '{filePath}'");

// XAudio2 is a non-blocking API, so wait for the buffer to finish
// playing before allowing the program to complete.
bool isPlaying = true;
while (isPlaying)
{
    isPlaying = voice.State.BuffersQueued != 0;
}

pcmDataStream.Dispose();
Console.WriteLine("Playback finished.");