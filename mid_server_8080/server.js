var ws = require('nodejs-websocket');

var packages = "";

var server = ws.createServer(function(connection){
    connection.on('text', function(str) {
           
        if(connection.client===undefined) {

            connection.client = str;
            console.log("This client is : ",connection.client);

            // if 'To' connect, server will send message every 35ms
            if(connection.client=="to"){
                setInterval(() => {
                    if(packages!="" && connection.readyState==1)    {
                        connection.send(packages);
                        packages = "";
                    }
                }, 35);
            }else{
                return;
            }
            
        } 

        if(connection.client == "from")
        {
            packages = str;
            console.log("server received : ",str);
        } 
    });
}).listen(8080); 