
echo ����csharp�ļ�����ĿGrpcImpls��Ŀ�У��������Ŀ���ɣ�

protoc.exe -I ./ --csharp_out ../gen-csharp  helloService.proto --grpc_out ../gen-csharp --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe

echo �������
pause

