[CmdletBinding()]
Param(
    [Parameter(Mandatory=$False)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

function Get-ScriptDirectory { 
    Split-Path -parent $PSCommandPath 
}

function Run-It () {
	$solution_file = "UCommerce.Transactions.Payments.sln";

	$scriptPath = Get-ScriptDirectory;

	$src = Resolve-Path "$scriptPath\..\src";

	foreach($element in Get-Registered-Payment-Providers){
		New-Item -Type Directory "$scriptPath\Target\$element\bin" -Force
		Copy-Item "$src/UCommerce.Transactions.Payments.$element\bin\$Configuration\UCommerce.Transactions.Payments.$element.dll" -Destination "$scriptPath\Target\$element\bin" -Force
		Copy-Item "$src/UCommerce.Transactions.Payments.$element\Configuration" -Destination "$scriptPath\Target\$element\Configuration" -Force -Recurse
	}
	
}

Function Get-Registered-Payment-Providers {

	$paymentProviders = @("Adyen","Authorizedotnet","Braintree","Dibs","EPay","EWay","GlobalCollect","Ideal","MultiSafepay","Netaxept","Ogone","Payer","PayEx","PayPal","Quickpay","SagePay","Schibsted","SecureTrading","WorldPay");

	return $paymentProviders;
}

Run-It