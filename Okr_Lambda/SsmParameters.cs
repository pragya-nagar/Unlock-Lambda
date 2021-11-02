using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;


namespace Okr_Lambda
{
    public interface ISsmParameters
    {
        string GetSecureParameter(string parameterName);
    }
    public class SsmParameters : ISsmParameters
    {
        private readonly IAmazonSimpleSystemsManagement _ssm;
        public SsmParameters(IAmazonSimpleSystemsManagement ssm)
        {
            _ssm = ssm;
        }

        public string GetSecureParameter(string parameterName)
        {
            var paramReq = new GetParameterRequest { WithDecryption = true, Name = parameterName };
            var paramResp1 = _ssm.GetParameterAsync(paramReq).GetAwaiter().GetResult();
            return paramResp1.Parameter.Value;
        }
    }
}
