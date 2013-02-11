<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="Settings.ascx.cs" Inherits="DotNetNuke.Modules.ActiveForumsTapatalk.Settings" %>
<%@ Register TagName="label" TagPrefix="dnn" Src="~/controls/labelcontrol.ascx" %>

<h2 id="dnnSitePanel-BasicSettings" class="dnnFormSectionHead"><a href="" class="dnnSectionExpanded"><%=LocalizeString("BasicSettings")%></a></h2>
<fieldset>
    <div class="dnnFormItem">
        <dnn:Label ID="lblEnabled" runat="server" Suffix=":"  /> 
        <asp:CheckBox ID="ckEnabled" runat="server" />&nbsp;&nbsp;&nbsp;
        <asp:LinkButton runat="server" ID="lbInstallHandler" OnClick="ToggleTapatalkAPIHandler" />
    </div>
    <div class="dnnFormItem">
        <dnn:Label ID="lblAFInstance" runat="server" Suffix=":"  /> 
        <asp:DropDownList ID="ddlAFInstance" runat="server" />
    </div>
    <div class="dnnFormItem">
        <dnn:label ID="lblAllowAnonymous" runat="server" Suffix=":"  />
        <asp:CheckBox runat="server" ID="ckAllowAnonymous" />
    </div>
    <div class="dnnFormItem">
        <dnn:label ID="lblRegistrationPage" runat="server" Suffix=":"  />
        <asp:TextBox runat="server" ID="txtRegistrationPage"></asp:TextBox>
    </div>
</fieldset>
<h2 id="H1" class="dnnFormSectionHead"><a href="" class="dnnSectionExpanded"><%=LocalizeString("TapatalkConfigurationInformation")%></a></h2>
<fieldset>
    <p><%=LocalizeString("TapatalkInfo")%></p>
    <div class="dnnFormItem">
        <dnn:Label ID="lblForumUrl" runat="server" Suffix=":"  /> 
        <asp:TextBox runat="server" ID="txtForumUrl" runat="server" ReadOnly="true"></asp:TextBox>
    </div>
    <div class="dnnFormItem">
        <dnn:Label ID="lblInstallationDirectoryName" runat="server" Suffix=":"  /> 
        <asp:TextBox runat="server" ID="txtInstallationDirectoryName" runat="server" ReadOnly="true"></asp:TextBox>
    </div>
    <div class="dnnFormItem">
        <dnn:Label ID="lblFileExtension" runat="server" Suffix=":"  /> 
        <asp:TextBox runat="server" ID="txtFileExtension" runat="server" ReadOnly="true" Text="ashx"></asp:TextBox>
    </div>
</fieldset>

