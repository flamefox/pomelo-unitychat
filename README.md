# pomelo-unitychat

a pomelo unity chat sample

* pure c# code
* support proto,dict cache(have some problem, soon fixed)
* poll mode only
* support tls
* use LitJson
* all callback at main thread

## Test with

unity 5.x
pomelo 1.2.2~

## install

replace the proto define of chatofpomelo with chatofpomelo-proto-patch
then run the server and client

## known issue

sometime unity socket BeginConnect will not return, re-compile script will fix this