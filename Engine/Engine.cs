using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Engine
{


    public class Engine
    {
        private int width, height;



        private enum enmTeamColor : byte
        {
            Black = 0,
            Red = 1,
            Green = 2
        }



        public Engine(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        public void CalcNewState(byte[] currentState, byte[] next)
        {
            //for (int ii = 0; ii < width * height; ii++)
            Parallel.For(0, width * height, new ParallelOptions() { MaxDegreeOfParallelism = 6 }, (ii, loopState) =>
            {
                int iNum;
                byte bySpawnColor;
                CalcNumOfNeghours(currentState, ii, out iNum, out bySpawnColor);

                if (iNum >= 4 || iNum < 2)
                {
                    next[ii] = (byte)enmTeamColor.Black; //kill
                }
                else if (iNum == 3)
                {
                    next[ii] = bySpawnColor; //spawn
                }
                else if (iNum == 2)
                {
                    next[ii] = currentState[ii]; //survive // copy
                }
                else
                {
                    throw new Exception("Should no reach here");
                }
            });



        }

        private void CalcNumOfNeghours(byte[] currentState, int ii, out int iNum, out byte bySpawnColor)
        {

            int X = ii / width;
            int Y = ii % width;
            int iRed = 0, iGreen = 0;


            for (int k = -1; k <= 1; k++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    int iPos = (X + k) * width + Y + j;
                    if (CheckIfValidPosition(X + k, Y + j) && currentState[iPos] == (byte)enmTeamColor.Green && !(k == 0 && j == 0))
                    {
                        iGreen++;
                    }
                    else if (CheckIfValidPosition(X + k, Y + j) && currentState[iPos] == (byte)enmTeamColor.Red && !(k == 0 && j == 0))
                    {
                        iRed++;
                    }
                }
            }


            iNum = iRed + iGreen;
            bySpawnColor = iGreen > iRed ? (byte)enmTeamColor.Green : (byte)enmTeamColor.Red;
        }



        private bool CheckIfValidPosition(int X, int Y)
        {
            return X >= 0 && X <= width - 1 && Y >= 0 && Y <= height - 1;
        }

        private int From2DTo1D(int i, int j)
        {
            return i * width + j;
        }
    }
}
