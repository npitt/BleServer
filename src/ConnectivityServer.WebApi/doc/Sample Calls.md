﻿== Write to characteristic

** CURL **

curl -X POST --header 'Content-Type: application/json-patch+json' --header 'Accept: application/json' -d '{ \ 
   "deviceUuid": "BluetoothLE#BluetoothLE5c:f3:70:8b:2c:d7-c9:f3:db:a6:39:61", \ 
   "serviceUuid": "6e400001-b5a3-f393-e0a9-e50e24dcca9e", \ 
   "characteristicUuid": "6e400002-b5a3-f393-e0a9-e50e24dcca9e", \ 
   "buffer": ["17", "39", "02", "09", "00", "5B", "01", "01", "01", "01", "00", "00", "00", "00", "00", "41", "43", "59", "43", "4C", "4F", "56", "49", "52", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "10", "27", "00", "00", "10", "27", "00", "00", "1E" \ 
   ] \ 
 }' 'http://localhost:56962/api/Gatt'

** Request body to insert to swagger **
{
  "deviceUuid": "BluetoothLE#BluetoothLE5c:f3:70:8b:2c:d7-c9:f3:db:a6:39:61",
  "serviceUuid": "6e400001-b5a3-f393-e0a9-e50e24dcca9e",
  "characteristicUuid": "6e400002-b5a3-f393-e0a9-e50e24dcca9e",
  "buffer": ["17", "39", "02", "09", "00", "5B", "01", "01", "01", "01", "00", "00", "00", "00", "00", "41", "43", "59", "43", "4C", "4F", "56", "49", "52", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "00", "10", "27", "00", "00", "10", "27", "00", "00", "1E"
  ]
}