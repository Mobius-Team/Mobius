<?php

include("databaseinfo.php");

// Attempt to connect to the database with the mutelist table
try {
    $db = new PDO("mysql:host=$DB_HOST;dbname=$DB_NAME", $DB_USER, $DB_PASSWORD);
    $db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
}
catch(PDOException $e)
{
    echo "Error connecting to the database with the mutelist table\n";
    file_put_contents('PDOErrors.txt', $e->getMessage() . "\n-----\n", FILE_APPEND);
    exit;
}


###################### No user serviceable parts below #####################

function get_error_message($result)
{
    global $db;

    if (!$result)
        return "";

    $errorInfo = $db->errorInfo();
    return $errorInfo[2];
}


#
# The XMLRPC server object
#

$xmlrpc_server = xmlrpc_server_create();

#
# Return list of muted agents and objects
#

xmlrpc_server_register_method($xmlrpc_server, "mutelist_request",
                              "mutelist_request");

function mutelist_request($method_name, $params, $app_data)
{
    global $db;

    $req        = $params[0];

    $avatarUUID = $req['avataruuid'];

    $query = $db->prepare("SELECT * FROM mutelist WHERE AgentID = ?");
    $result = $query->execute( array($avatarUUID) );

    $mutelist = "";
    if ($query->rowCount() > 0)
    {
        while ($row = $query->fetch(PDO::FETCH_ASSOC))
        {
            $mutelist .= $row["type"] . " ";
            $mutelist .= $row["MuteID"] . " ";
            $mutelist .= $row["MuteName"] . "|";
            $mutelist .= $row["flags"] . "\n";
        }
    }

    $response_xml = xmlrpc_encode(array(
        'success'      => $result,
        'errorMessage' => get_error_message($result),
        'mutelist'     => $mutelist
    ));

    print $response_xml;
}

#
# Remove an event notify reminder request
#

xmlrpc_server_register_method($xmlrpc_server, "mutelist_update",
                              "mutelist_update");

function mutelist_update($method_name, $params, $app_data)
{
    global $db;

    $req        = $params[0];

    $avatarUUID = $req['avataruuid'];
    $muteUUID   = $req['muteuuid'];
    $name       = $req['name'];
    $type       = $req['type'];
    $flags      = $req['flags'];

    $query = $db->prepare("INSERT INTO mutelist VALUES (?, ?, ?, ?, ?, NOW())");
    $result = $query->execute(
                    array($avatarUUID, $muteUUID, $name, $type, $flags) );

    $response_xml = xmlrpc_encode(array(
        'success'      => $result,
        'errorMessage' => get_error_message($result)
    ));

    print $response_xml;
}

#
# Remove an event notify reminder request
#

xmlrpc_server_register_method($xmlrpc_server, "mutelist_remove",
                              "mutelist_remove");

function mutelist_remove($method_name, $params, $app_data)
{
    global $db;

    $req        = $params[0];

    $avatarUUID = $req['avataruuid'];
    $muteUUID   = $req['muteuuid'];

    $query = $db->prepare("DELETE FROM mutelist WHERE" .
                          " AgentID = ? AND MuteID = ?");
    $result = $query->execute( array($avatarUUID, $muteUUID) );

    $response_xml = xmlrpc_encode(array(
        'success'      => $result,
        'errorMessage' => get_error_message($result)
    ));

    print $response_xml;
}

#
# Process the request
#

$request_xml = file_get_contents("php://input");

xmlrpc_server_call_method($xmlrpc_server, $request_xml, '');
xmlrpc_server_destroy($xmlrpc_server);
?>
