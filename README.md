# pomelo-unitychat

a pomelo unity chat sample

* pure c# code
* support proto,dict cache
* poll mode only and all delegate at main thread
* support tls
* use LitJson

[pomelo's proto generator](https://github.com/flamefox/pomeloc)

## Test with

unity 5.x
pomelo 1.2.2
Windows

## install

replace the proto define of chatofpomelo with chatofpomelo-proto-patch
then run the server and client

## known issue

sometime unity socket BeginConnect will not return(maybe Unity Editor's bug), re-compile script will fix this
[TCP Socket Async BeginSend never happens](http://answers.unity3d.com/questions/892371/tcp-socket-async-beginsend-never-happens.html)