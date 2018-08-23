
#include <memory.h>
#include "Engine.h"
const int sizeOfColArray = 2;
unsigned char cColors[sizeOfColArray];
int iWidth, iHeight;

enum enmTeamColor : unsigned char
{
	Black = 0,
	Red = 1,
	Green = 2
};


extern "C"
int Init(int iwidth, int iheight)
{
	iWidth = iwidth;
	iHeight = iheight;
	return iWidth*iHeight;
}

bool CheckIfValidPosition(int X, int Y)
{
	if (X >= 0 && X <= iWidth - 1 && Y >= 0 && Y <= iHeight - 1)
	{
		return true;
	}
	else
	{
		return false;
	}
}


void CalcNumOfNeghours(unsigned char *currentState, int ii, int *iNum,unsigned char *bySpawnColor)
{

	int X = ii / iWidth;
	int Y = ii % iWidth;
	int iRed = 0, iGreen = 0;
		
	for (int k = -1; k <= 1; k++)
	{
		for (int j = -1; j <= 1; j++)
		{
			int iPos = (X + k) * iWidth + Y + j;
			if (CheckIfValidPosition(X + k, Y + j) && currentState[iPos] ==enmTeamColor::Green && !(k == 0 && j == 0))
			{
				iGreen++;
			}
			else if (CheckIfValidPosition(X + k, Y + j) && currentState[iPos] == enmTeamColor::Red && !(k == 0 && j == 0))
			{
				iRed++;
			}
		}
	}

	*iNum = iRed + iGreen;
	*bySpawnColor = (iGreen > iRed) ? enmTeamColor::Green : enmTeamColor::Red;
}

extern "C"
void CalcNewState(unsigned char *curr, unsigned char *nxt)
{
	//char* ptrArr1 = &arr1[0];
	//char* ptrArr2 = &arr2[0];
	//*ptrArr1 = 20;
	//*ptrArr2 = 30;
	int iNum=0;
	
	for (int ii = 0; ii < iWidth * iHeight; ii++)
	{
		unsigned char cCol = 0;
		CalcNumOfNeghours(curr, ii, &iNum, &cCol);

		if (iNum >= 4 || iNum < 2)
		{
			nxt[ii] = enmTeamColor::Black; //kill
		}
		else if (iNum == 3)
		{
			nxt[ii] = cCol; //spawn
		}
		else if (iNum == 2)
		{
			nxt[ii] = curr[ii]; //survive // copy
		}

	}
}





