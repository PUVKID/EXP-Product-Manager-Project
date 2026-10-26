using System;
using System.Drawing;
using System.Windows.Forms;
namespace BunjangAuto {
    public static class ShippingOptions {
        public static string CarrierLabel(string code){return code=="GS_HALF_PRICE"?"GS25 만":code=="CU_THRIFTY"?"CU만":"둘 다 가능";}
        public static void Validate(Product p){
            if(p.FreeShipping)return;
            if(!p.ShippingFee.HasValue||p.ShippingFee<100||p.ShippingFee>30000)throw new InvalidOperationException("일반택배 배송비를 100~30,000원으로 입력해 주세요. 상품 편집의 배송 설정에서 변경할 수 있습니다.");
            if(p.HalfShipping){
                if(!p.HalfShippingFee.HasValue||p.HalfShippingFee<0||p.HalfShippingFee>30000)throw new InvalidOperationException("반값택배 요금을 입력해 주세요.");
                if(p.HalfShippingCarrier!="ALL"&&p.HalfShippingCarrier!="GS_HALF_PRICE"&&p.HalfShippingCarrier!="CU_THRIFTY")throw new InvalidOperationException("반값택배 편의점을 선택해 주세요.");
            }
        }
    }
    public class ShippingEditor : SoftCard {
        readonly CheckBox included,remember;
        readonly RadioButton halfNo,halfYes;
        readonly SoftInput normal,halfFee;
        readonly SoftCombo carrier;
        public ShippingEditor(Product p){
            Size=new Size(972,278);
            Controls.Add(Design.Label("배송 설정",24,18,400,30,15,"t"));
            included=new CheckBox{Name="shippingIncluded",Text="배송비 포함",Left=744,Top=23,Width=200,Height=26,Checked=p.FreeShipping};
            Controls.Add(Design.Label("일반택배 · 100~30,000원",24,70,300,24,10,"m"));
            normal=new SoftInput{Name="shippingFee",Left=24,Top=100,Width=300,Height=42,Text=p.ShippingFee.HasValue?p.ShippingFee.Value.ToString():""};
            Controls.Add(Design.Label("반값택배",368,70,300,24,10,"m"));
            halfNo=new RadioButton{Name="halfShippingNo",Text="불가능",Left=368,Top=108,Width=104,Height=26,Checked=!p.HalfShipping};
            halfYes=new RadioButton{Name="halfShippingYes",Text="가능",Left=484,Top=108,Width=104,Height=26,Checked=p.HalfShipping};
            Controls.Add(Design.Label("반값택배 편의점",24,160,300,24,10,"m"));
            carrier=new SoftCombo{Name="halfShippingCarrier",Left=24,Top=190,Width=300};carrier.Items.AddRange(new object[]{"둘 다 가능","GS25 만","CU만"});carrier.SelectedIndex=p.HalfShippingCarrier=="GS_HALF_PRICE"?1:p.HalfShippingCarrier=="CU_THRIFTY"?2:0;
            Controls.Add(Design.Label("반값택배 요금 · 원",368,160,300,24,10,"m"));
            halfFee=new SoftInput{Name="halfShippingFee",Left=368,Top=190,Width=260,Height=42,Text=p.HalfShippingFee.HasValue?p.HalfShippingFee.Value.ToString():""};
            remember=new CheckBox{Name="rememberShipping",Text="다음 등록시에도 사용",Left=664,Top=198,Width=282,Height=28,Checked=p.RememberShipping};
            Controls.AddRange(new Control[]{included,normal,halfNo,halfYes,carrier,halfFee,remember});
            included.CheckedChanged+=delegate{RefreshFields();};halfYes.CheckedChanged+=delegate{RefreshFields();};RefreshFields();
        }
        void RefreshFields(){normal.Enabled=!included.Checked;halfNo.Enabled=halfYes.Enabled=!included.Checked;carrier.Enabled=halfFee.Enabled=!included.Checked&&halfYes.Checked;}
        static int? Amount(string text){int value;return int.TryParse((text??"").Replace(",","").Trim(),out value)?(int?)value:null;}
        public void Save(Product p){p.FreeShipping=included.Checked;p.ShippingFee=Amount(normal.Text);p.HalfShipping=halfYes.Checked;p.HalfShippingCarrier=carrier.SelectedIndex==1?"GS_HALF_PRICE":carrier.SelectedIndex==2?"CU_THRIFTY":"ALL";p.HalfShippingFee=Amount(halfFee.Text);p.RememberShipping=remember.Checked;}
        public void ValidateValues(){var p=new Product();Save(p);ShippingOptions.Validate(p);}
    }
}
