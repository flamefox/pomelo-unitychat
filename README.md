# pomelo-unitychat

a pomelo unity chat sample

* pure c# code
* support proto,dict cache
* poll mode only and all delegate at main thread
* support tls
* use LitJson

[pomelo's proto generator](https://github.com/flamefox/pomeloc)

## TLS/SSL EASY USE

client:
```C#
pomeloBehaviour client;
client.ConnectServer(host, port, Pomelo.DotNetClient.ClientProtocolType.TLS);
```

server: 
```javascript
//app.js
	app.set('connectorConfig',
		{
			connector: pomelo.connectors.hybridconnector,
			useDict: true,

			// enable useProto
			useProtobuf: true

			,ssl: {
				ca: [fs.readFileSync('./keys/out/CA/ca.crt')],
				pfx: fs.readFileSync('./keys/out/newcert/server.pfx'),
				// This is necessary only if using the client certificate authentication.
				//requestCert: true,
				//rejectUnauthorized: true
			}
		});
```

if you want change the verify of the server, change the code of TransporterSSL.ValidateServerCertificate


## Test with

unity 5.x
pomelo 1.2.2
Windows

## install

replace the proto define of chatofpomelo with chatofpomelo-proto-patch
then run the server and client

## known issue

sometime unity socket BeginConnect will not return(maybe Unity Editor's bug[TCP Socket Async BeginSend never happens](http://answers.unity3d.com/questions/892371/tcp-socket-async-beginsend-never-happens.html)), re-compile script or restart will fix this
