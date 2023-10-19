namespace RemotePhone.Models
{
    ///<summary>
    ///通用状态回复
    ///</summary>
    public class StatusResponse
    {
        ///<summary>
        ///Success或Fail
        ///</summary>
        public string? Status { set; get; }
        ///<summary>
        ///消息
        ///</summary>
        public string? Message { set; get; }
    }

    public class ListStatusResponse:StatusResponse
    {
        ///<summary>
        ///成功文件
        ///</summary>
        public List<string>? Success { set; get; }
        ///<summary>
        ///失败文件
        ///</summary>
        public List<string>? Fail { set; get; }
    }

    public class PortStatusResponse: StatusResponse
    {
        ///<summary>
        ///成功文件
        ///</summary>
        public string? UserId { set; get; }
    }
}
