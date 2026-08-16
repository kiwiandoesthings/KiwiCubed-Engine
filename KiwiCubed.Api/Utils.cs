namespace KiwiCubed.Api;

using Arch.Core;
using LiteNetLib.Utils;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

using static Block;
using static Globals;

public static class Utils {
	public readonly struct IntVector3 : IEquatable<IntVector3>, IEquatable<Vector3> {
		public readonly int X;
		public readonly int Y;
		public readonly int Z;
		public static readonly IntVector3 Zero = new IntVector3(0, 0, 0);

		public int this[int index] {
			get {
				if (index == 0) {
					return X;
				} else if (index == 1) {
					return Y;
				} else if (index == 2) {
					return Z;
				}

				throw new IndexOutOfRangeException("Tried to access index {" + index + "} of an IntVector3");
			}
		}

		public IntVector3(int x, int y, int z) {
			X = x;
			Y = y;
			Z = z;
		}

		public IntVector3(float x, float y, float z) {
			X = (int)x;
			Y = (int)y;
			Z = (int)z;
		}

		public IntVector3(Vector3 vector) {
			X = (int)vector.X;
			Y = (int)vector.Y;
			Z = (int)vector.Z;
		}

		public IntVector3(int value) {
			X = value;
			Y = value;
			Z = value;
		}

        public IntVector3 PositiveModulo(int modulator) {
            return new IntVector3(Utils.PositiveModulo((int)X, modulator), Utils.PositiveModulo((int)Y, modulator), Utils.PositiveModulo((int)Z, modulator));
        }

        public IntVector3 FloorDiv(int divisor) {
            return new IntVector3(Utils.FloorDiv((int)X, divisor), Utils.FloorDiv((int)Y, divisor), Utils.FloorDiv((int)Z, divisor));
        }

        public IntVector3 Max(IntVector3 other) {
			return new IntVector3((X < other.X) ? other.X : X, (Y < other.Y) ? other.Y : Y, (Z < other.Z) ? other.Z : Z);
		}

		public IntVector3 Min(IntVector3 other) {
			return new IntVector3((X > other.X) ? other.X : X, (Y > other.Y) ? other.Y : Y, (Z > other.Z) ? other.Z : Z);
		}

		public IntVector3 Abs() {
			return new IntVector3(Math.Abs(X), Math.Abs(Y), Math.Abs(Z));
		}

		public static IntVector3 operator +(IntVector3 a, IntVector3 b) {
			return new IntVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
		}

		public static IntVector3 operator *(IntVector3 a, IntVector3 b) {
			return new IntVector3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
		}

		public static IntVector3 operator -(IntVector3 a, IntVector3 b) {
			return new IntVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
		}

		public static IntVector3 operator /(IntVector3 a, IntVector3 b) {
			return new IntVector3((int)Math.Floor(a.X / (double)b.X), (int)Math.Floor(a.Y / (double)b.Y), (int)Math.Floor(a.Z / (double)b.Z));
		}

		public static IntVector3 operator %(IntVector3 a, IntVector3 b) {
			return new IntVector3(a.X % b.X, a.Y % b.Y, a.Z % b.Z);
		}

		public static IntVector3 operator &(IntVector3 a, IntVector3 b) {
			return new IntVector3(a.X & b.X, a.Y & b.Y, a.Z & b.Z);
		}

		public static IntVector3 operator +(IntVector3 a, float modifier) {
			return new IntVector3(a.X + modifier, a.Y + modifier, a.Z + modifier);
		}

		public static IntVector3 operator *(IntVector3 a, float modifier) {
			return new IntVector3(a.X * modifier, a.Y * modifier, a.Z * modifier);
		}

		public static IntVector3 operator -(IntVector3 a, float modifier) {
			return new IntVector3(a.X - modifier, a.Y - modifier, a.Z - modifier);
		}

		public static IntVector3 operator /(IntVector3 a, float modifier) {
			return new IntVector3((int)Math.Floor(a.X / (double)modifier), (int)Math.Floor(a.Y / (double)modifier), (int)Math.Floor(a.Z / (double)modifier));
		}

		public static IntVector3 operator %(IntVector3 a, int modifier) {
			return new IntVector3(a.X % modifier, a.Y % modifier, a.Z % modifier);
		}

		public static IntVector3 operator &(IntVector3 a, int modifier) {
			return new IntVector3(a.X & modifier, a.Y & modifier, a.Z & modifier);
		}

		public static bool operator ==(IntVector3 a, IntVector3 b) {
			return a.Equals(b);
		}

		public static bool operator !=(IntVector3 a, IntVector3 b) {
			return !a.Equals(b);
		}

		public static bool operator ==(IntVector3 a, Vector3 b) {
			return a.Equals(b);
		}

		public static bool operator !=(IntVector3 a, Vector3 b) {
			return !a.Equals(b);
		}

		public bool Equals(IntVector3 other) {
			return X == other.X && Y == other.Y && Z == other.Z;
		}

		public bool Equals(Vector3 other) {
			return X == other.X && Y == other.Y && Z == other.Z;
		}

		public override bool Equals(object? obj) {
			return obj is IntVector3 other && obj is not null && Equals(other);
		}

		public Vector3 ToVector3() {
			return new Vector3(X, Y, Z);
		}

		public override int GetHashCode() {
			return HashCode.Combine(X, Y, Z);
		}

		public override string ToString() {
			return "{" + X + ", " + Y + ", " + Z + "}";
		}

		static IntVector3() {
			Zero = new IntVector3(0, 0, 0);
		}

		public bool IsOnChunkEdge() {
            return X == 0 || Y == 0 || Z == 0 || X == chunkEdge || Y == chunkEdge - 1 || Z == chunkEdge - 1;
		}

		public bool IsInSquareRadiusOf(IntVector3 radiusOrigin, int radius) {
			return Math.Abs(X - radiusOrigin.X) <= radius && Math.Abs(Y - radiusOrigin.Y) <= radius && Math.Abs(Z - radiusOrigin.Z) <= radius;
		}

        public bool IsInCircularRadiusOf(IntVector3 radiusOrigin, float radius) {
            int distanceX = X - radiusOrigin.X;
            int distanceY = Y - radiusOrigin.Y;
            int distanceZ = Z - radiusOrigin.Z;

            return (distanceX * distanceX + distanceY * distanceY + distanceZ * distanceZ) <= (radius * radius);
        }
    }

	public struct FullBlockPosition {
		public IntVector3 blockPosition;
		public IntVector3 chunkPosition;

        public FullBlockPosition(IntVector3 blockPosition, IntVector3 chunkPosition) {
            this.blockPosition = blockPosition;
            this.chunkPosition = chunkPosition;
        }

        public void AddBlockPosition(IntVector3 modifier) {
			IntVector3 newBlockPosition = blockPosition + modifier;
			chunkPosition += newBlockPosition / 32f;
			blockPosition = newBlockPosition & 31;
		}

        public readonly IntVector3 ToIntVector3() {
			return blockPosition + (chunkPosition * chunkSize);
		}

        public readonly Vector3 ToVector3() {
			IntVector3 fullPosition = ToIntVector3();
			return new Vector3(fullPosition.X, fullPosition.Y, fullPosition.Z);
		}

        public readonly override int GetHashCode() {
			return HashCode.Combine(blockPosition.GetHashCode(), chunkPosition.GetHashCode());
		}

        public readonly override string ToString() {
			IntVector3 fullyQualifiedBlockPosition = chunkPosition * 32 + blockPosition;
			return "Block: " + blockPosition + ", chunk: " +  chunkPosition + ", full: " + fullyQualifiedBlockPosition;
		}

        public readonly void Serialize(NetDataWriter writer) {
            writer.Put(blockPosition.X);
            writer.Put(blockPosition.Y);
            writer.Put(blockPosition.Z);
            writer.Put(chunkPosition.X);
            writer.Put(chunkPosition.Y);
            writer.Put(chunkPosition.Z);
        }

		public static FullBlockPosition Deserialize(NetDataReader reader) {
            int blockX = reader.GetInt();
            int blockY = reader.GetInt();
            int blockZ = reader.GetInt();
            int chunkX = reader.GetInt();
            int chunkY = reader.GetInt();
            int chunkZ = reader.GetInt();
            IntVector3 blockPosition = new IntVector3(blockX, blockY, blockZ);
            IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
            return new FullBlockPosition(blockPosition, chunkPosition);
        }
    }

	public struct BlockRayHit {
		public bool hit;
		public FullBlockPosition blockHitPosition;
		public FaceDirection faceHitIndex;

		public BlockRayHit() {
			blockHitPosition = default;
			faceHitIndex = FaceDirection.LEFT;
			hit = false;
		}

		public BlockRayHit(FullBlockPosition blockHitPosition) {
			hit = false;
			faceHitIndex = FaceDirection.LEFT;
			this.blockHitPosition = blockHitPosition;
		}
	}

	public struct BoundingBox {
		private Vector3 corner1 = Vector3.Zero;
		private Vector3 corner2 = Vector3.Zero;

		public BoundingBox(Vector3 corner1, Vector3 corner2) {
			this.corner1 = corner1;
			this.corner2 = corner2;
		}

        public readonly Vector3 Corner1() {
			return corner1;
		}

        public readonly Vector3 Corner2() {
			return corner2;
		}

		public void Resize(Vector3 corner1, Vector3 corner2) {
			this.corner1 = corner1;
			this.corner2 = corner2;
		}

        public readonly float GetWidth() {
			return Math.Abs(corner1.X - corner2.X);
		}

        public readonly float GetHeight() {
			return Math.Abs(corner1.Y - corner2.Y);
		}

        public readonly float GetLength() {
			return Math.Abs(corner1.Z - corner2.Z);
		}

		public readonly Vector3 Midpoint() {
			return new Vector3((corner1.X + corner2.X) / 2.0f, (corner1.Y + corner2.Y) / 2.0f, (corner1.Z + corner2.Z) / 2.0f);
		}
	}

	public static float Lerp(float value1, float value2, float time) {
		return value1 + time * (value2 - value1);
	}

    public static int ReadIntFromBuffer(byte[] buffer, ref int offset) {
        int val = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24);
        offset += 4;
        return val;
    }

    public static void WriteIntToBuffer(byte[] buffer, ref int offset, int value) {
        buffer[offset++] = (byte)(value & 0xFF);
        buffer[offset++] = (byte)((value >> 8) & 0xFF);
        buffer[offset++] = (byte)((value >> 16) & 0xFF);
        buffer[offset++] = (byte)((value >> 24) & 0xFF);
    }

    public static ushort ReadUshortFromBuffer(byte[] buffer, ref int offset) {
        ushort val = (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        offset += 2;
        return val;
    }

    public static void WriteUshortToBuffer(byte[] buffer, ref int offset, ushort value) {
        buffer[offset++] = (byte)(value & 0xFF);
        buffer[offset++] = (byte)((value >> 8) & 0xFF);
    }

    public static int PositiveModulo(float value, int modulator) {
        int newValue = (int)Math.Floor(value);
        int result = newValue % modulator;
        return (result < 0) ? result + modulator : result;
    }

    public static int FloorDiv(float value, int divisor) {
        int newValue = (int)Math.Floor(value);
        int result = newValue / divisor;
        if (value < 0 && newValue % divisor != 0) {
            result -= 1;
        }
        return result;
    }

    public static Vector3 PositiveModulo(Vector3 value, int modulator) {
		return new Vector3(PositiveModulo(value.X, modulator), PositiveModulo(value.Y, modulator), PositiveModulo(value.Z, modulator));
    }

    public static Vector3 FloorDiv(Vector3 value, int divisor) {
        return new Vector3(FloorDiv(value.X, divisor), FloorDiv(value.Y, divisor), FloorDiv(value.Z, divisor));
    }

	public struct SimpleTransform {
		public Vector3 position = Vector3.Zero;
		public Quaternion orientation = Quaternion.Identity;

        public SimpleTransform(Vector3 position) {
            this.position = position;
        }

        public SimpleTransform(Vector3 position, Quaternion orientation) {
			this.position = position;
			this.orientation = orientation;
		}

        public readonly void Serialize(NetDataWriter writer) {
			writer.Put(position.X);
			writer.Put(position.Y);
			writer.Put(position.Z);
			writer.Put(orientation.X);
			writer.Put(orientation.Y);
			writer.Put(orientation.Z);
			writer.Put(orientation.W);
		}

		public void Deserialize(NetDataReader reader) {
			position.X = reader.GetFloat();
            position.Y = reader.GetFloat();
            position.Z = reader.GetFloat();
			orientation.X = reader.GetFloat();
            orientation.Y = reader.GetFloat();
            orientation.Z = reader.GetFloat();
            orientation.W = reader.GetFloat();
        }
	}

    public static ulong MakeAUID(string playerName) {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes("kiwicubed:" + playerName));
        ulong low = BitConverter.ToUInt64(hash, 0);
        ulong high = BitConverter.ToUInt64(hash, 8);

        return low ^ high;
    }

    public static ulong MakeRandomAUID() {
        byte[] randomBytes = new byte[16];
        RandomNumberGenerator.Fill(randomBytes);
        ulong low = BitConverter.ToUInt64(randomBytes, 0);
        ulong high = BitConverter.ToUInt64(randomBytes, 8);

        return low ^ high;
    }

    public static ComponentType[] With(this ComponentType[] baseTypes, params ComponentType[] extras) {
        return baseTypes.Concat(extras).ToArray();
    }

    public static bool TryGetKeyByValue<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TValue value, out TKey? key) where TKey : notnull {
        foreach (KeyValuePair<TKey, TValue> pair in dictionary) {
            if (EqualityComparer<TValue>.Default.Equals(pair.Value, value)) {
                key = pair.Key;
                return true;
            }
        }
        key = default;
        return false;
    }

    public static bool TryGetKeyByValue<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TValue value, out TKey? key) where TKey : notnull {
        foreach (KeyValuePair<TKey, TValue> pair in dictionary) {
            if (EqualityComparer<TValue>.Default.Equals(pair.Value, value)) {
                key = pair.Key;
                return true;
            }
        }
        key = default;
        return false;
    }

	public static byte[] AsByteArray(this ushort[] originalArray) {
		byte[] byteArray = new byte[originalArray.Length * 2];
		Buffer.BlockCopy(originalArray, 0, byteArray, 0, byteArray.Length);
		return byteArray;
    }

	public static ushort[] AsUshortArray(this byte[] originalArray) {
        ushort[] ushortArray = new ushort[originalArray.Length / 2];
        Buffer.BlockCopy(originalArray, 0, ushortArray, 0, originalArray.Length);
        return ushortArray;
    }

    public class CircularBuffer<T> {
		private readonly T[] buffer;
		private readonly int size;
        private int head;
        private int count;

        public CircularBuffer(int size) {
            buffer = new T[size];
            this.size = size;
        }

        public CircularBuffer(int size, T fillWithValue) {
			buffer = new T[size];
			this.size = size;

			Array.Fill<T>(buffer, fillWithValue);
			count = size;
		}

        public void Push(T newElement) {
            head = (head + 1) % size;
            buffer[head] = newElement;

			if (count < size) {
				count++;
			}
        }

        public T Get(int index) {
			ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count, nameof(index));

			int actualIndex = (head - index + size) % size;

			return buffer[actualIndex];
		}
    }
}