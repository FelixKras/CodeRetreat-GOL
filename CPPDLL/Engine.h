#pragma once

class _declspec(dllexport) Engine
{
public:
	int Init(int iw, int ih, unsigned char *cCol);
	void CalcNewState(unsigned char *arr1, unsigned char *arr2);

};
