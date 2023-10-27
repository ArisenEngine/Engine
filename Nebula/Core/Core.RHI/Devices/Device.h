#pragma once
#include<string>
#include<iostream>
namespace RHI {

	class Device
	{

		public:

			/**
			 * Queries the underlying OS version.
			 * @return The OS version.
			*/
			virtual int GetOSVersion() const noexcept = 0;

			virtual std::string GetPlatformName() const noexcept = 0;

			virtual ~Device() noexcept { std::cout << "~Device()" << std::endl; }

		protected:


	};

}